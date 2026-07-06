using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BillSoft.Application.Auth;
using BillSoft.Application.Vendors;
using BillSoft.Domain.Auditing;
using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Security;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using BillSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BillSoft.Infrastructure.Vendors;

public sealed class VendorBillOcrService : IVendorBillOcrService
{
    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "application/pdf"
    ];

    private readonly BillSoftDbContext _context;
    private readonly IVendorService _vendorService;
    private readonly IUploadedDocumentStorage _documentStorage;
    private readonly IVendorBillOcrProvider _ocrProvider;
    private readonly OcrOptions _ocrOptions;

    public VendorBillOcrService(
        BillSoftDbContext context,
        IVendorService vendorService,
        IUploadedDocumentStorage documentStorage,
        IVendorBillOcrProvider ocrProvider,
        IOptions<OcrOptions> ocrOptions)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _vendorService = vendorService ?? throw new ArgumentNullException(nameof(vendorService));
        _documentStorage = documentStorage ?? throw new ArgumentNullException(nameof(documentStorage));
        _ocrProvider = ocrProvider ?? throw new ArgumentNullException(nameof(ocrProvider));
        _ocrOptions = ocrOptions?.Value ?? throw new ArgumentNullException(nameof(ocrOptions));
    }

    public async Task<VendorBillOcrDraftListResponse> ListDraftsAsync(AuthUserContext currentUser, VendorBillOcrDraftListQuery query, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var branchFilter = ResolveBranchFilter(currentUser, query.BranchId);

        var draftsQuery = _context.VendorBillOcrDrafts
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId);

        if (branchFilter.HasValue)
        {
            draftsQuery = draftsQuery.Where(entity => entity.BranchId == branchFilter.Value);
        }
        else if (currentUser.BranchId.HasValue)
        {
            draftsQuery = draftsQuery.Where(entity => entity.BranchId == currentUser.BranchId.Value);
        }

        var drafts = await draftsQuery
            .ToListAsync(cancellationToken);

        return new VendorBillOcrDraftListResponse(
            drafts.OrderByDescending(entity => entity.CreatedAtUtc)
                .Select(ToListItem)
                .ToArray());
    }

    public async Task<VendorBillOcrDraftDetail> GetDraftAsync(AuthUserContext currentUser, Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await LoadDraftAsync(RequireRestaurantScope(currentUser), draftId, cancellationToken);
        EnsureBranchAccess(currentUser, draft.BranchId);
        return await ToDetailAsync(currentUser, draft, cancellationToken);
    }

    public async Task<VendorBillOcrDraftDetail> UploadDraftAsync(
        AuthUserContext currentUser,
        Guid? branchId,
        string originalFileName,
        string contentType,
        Stream fileContent,
        long fileSizeBytes,
        CancellationToken cancellationToken)
    {
        if (fileContent is null)
        {
            throw new InvalidOperationException("Uploaded file is required.");
        }

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new InvalidOperationException("Uploaded file name is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only JPEG, PNG, or PDF files are allowed.");
        }

        if (fileSizeBytes <= 0)
        {
            throw new InvalidOperationException("Uploaded file is required.");
        }

        if (fileSizeBytes > _ocrOptions.MaxUploadBytes)
        {
            throw new InvalidOperationException("Uploaded file is too large.");
        }

        var restaurantId = RequireRestaurantScope(currentUser);
        var resolvedBranchId = ResolveBranchScope(currentUser, branchId);
        var branch = await LoadBranchAsync(restaurantId, resolvedBranchId, requireActive: true, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var uploadedByUserId = RequireUserId(currentUser);

        await using var buffer = new MemoryStream();
        await fileContent.CopyToAsync(buffer, cancellationToken);
        var fileBytes = buffer.ToArray();
        if (fileBytes.Length == 0)
        {
            throw new InvalidOperationException("Uploaded file is required.");
        }

        await using var storageStream = new MemoryStream(fileBytes, writable: false);
        var storedDocument = await _documentStorage.SaveAsync(originalFileName, contentType, storageStream, cancellationToken);

        var draft = new VendorBillOcrDraft
        {
            RestaurantId = restaurantId,
            BranchId = branch.BranchId,
            UploadedByUserId = uploadedByUserId,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredFilePath = storedDocument.RelativePath,
            ContentType = contentType,
            FileSizeBytes = storedDocument.FileSizeBytes,
            Status = VendorBillOcrDraftStatus.Uploaded,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var providerStream = new MemoryStream(fileBytes, writable: false);
        VendorBillOcrProviderResult providerResult;
        try
        {
            providerResult = await _ocrProvider.ExtractAsync(
                new VendorBillOcrProviderRequest(
                    providerStream,
                    storedDocument.OriginalFileName,
                    contentType,
                    storedDocument.FileSizeBytes,
                    restaurantId,
                    branch.BranchId,
                    draft.VendorBillOcrDraftId),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            providerResult = CreateProviderFailure(MapProviderException(ex));
        }

        if (providerResult.IsSuccess && providerResult.Extraction is not null)
        {
            ApplyExtraction(draft, providerResult, restaurantId, branch.BranchId, now);
        }
        else
        {
            draft.Status = VendorBillOcrDraftStatus.ExtractionFailed;
            draft.SafeErrorMessage = NormalizeOptionalText(providerResult.SanitizedErrorMessage ?? "OCR extraction failed.", 500);
            draft.ProviderWarningsJson = providerResult.Warnings.Count == 0 ? null : Serialize(providerResult.Warnings);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        _context.VendorBillOcrDrafts.Add(draft);
        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "VendorBillOcrDraft.Uploaded",
            reason: "OCR draft uploaded.",
            entityType: "VendorBillOcrDraft",
            entityId: draft.VendorBillOcrDraftId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(new
            {
                draft.VendorBillOcrDraftId,
                draft.OriginalFileName,
                draft.ContentType,
                draft.FileSizeBytes,
                draft.Status,
                draft.ExtractedVendorName,
                draft.ExtractedBillNumber,
                draft.ExtractedBillDate,
                draft.ExtractedTotalAmount,
                draft.ExtractedConfidenceScore,
                draft.ProviderWarningsJson,
                LineCount = draft.Lines.Count
            }),
            createdAt: now);

        if (providerResult.IsSuccess && providerResult.Extraction is not null)
        {
            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "VendorBillOcrDraft.Extracted",
                reason: providerResult.Warnings.Count == 0 ? "OCR extraction completed." : "OCR extraction completed with warnings.",
                entityType: "VendorBillOcrDraft",
                entityId: draft.VendorBillOcrDraftId.ToString(),
                oldValueJson: null,
                newValueJson: Serialize(new
                {
                    draft.VendorBillOcrDraftId,
                    draft.ExtractedVendorName,
                    draft.ExtractedBillNumber,
                    draft.ExtractedBillDate,
                    draft.ExtractedTotalAmount,
                    draft.ExtractedConfidenceScore,
                    Warnings = providerResult.Warnings,
                    Lines = draft.Lines.Select(line => new
                    {
                        line.LineNumber,
                        line.ExtractedDescription,
                        line.ExtractedQuantity,
                        line.ExtractedUnitCost,
                        line.ExtractedLineTotal,
                        line.ConfidenceScore
                    })
                }),
                createdAt: now);
        }
        else
        {
            AddAudit(
                actor: currentUser,
                restaurant: restaurant,
                branch: branch,
                action: "VendorBillOcrDraft.ExtractionFailed",
                reason: draft.SafeErrorMessage ?? "OCR extraction failed.",
                entityType: "VendorBillOcrDraft",
                entityId: draft.VendorBillOcrDraftId.ToString(),
                oldValueJson: null,
                newValueJson: Serialize(new
                {
                    draft.VendorBillOcrDraftId,
                    draft.Status,
                    draft.SafeErrorMessage,
                    providerResult.SanitizedErrorCode
                }),
                createdAt: now);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToDetailAsync(currentUser, draft, cancellationToken);
    }

    public async Task<VendorBillOcrDraftDetail> UpdateDraftAsync(
        AuthUserContext currentUser,
        Guid draftId,
        UpdateVendorBillOcrDraftRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var restaurantId = RequireRestaurantScope(currentUser);
        var draft = await LoadTrackedDraftAsync(restaurantId, draftId, cancellationToken);
        EnsureDraftEditable(draft);
        EnsureBranchAccess(currentUser, draft.BranchId);

        var branch = await LoadBranchAsync(restaurantId, draft.BranchId, requireActive: false, cancellationToken);
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var before = await ToDetailAsync(currentUser, draft, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        draft.ReviewedVendorId = request.ReviewedVendorId;
        draft.ReviewedBillNumber = NormalizeOptionalText(request.ReviewedBillNumber, 40);
        draft.ReviewedBillDate = request.ReviewedBillDate?.Date;
        draft.ReviewedTotalAmount = request.ReviewedTotalAmount;
        draft.Status = VendorBillOcrDraftStatus.Extracted;
        draft.SafeErrorMessage = null;
        draft.UpdatedAtUtc = now;

        if (request.RemovedLineIds is not null && request.RemovedLineIds.Count > 0)
        {
            var removedLineIds = new HashSet<Guid>(request.RemovedLineIds);
            draft.Lines = draft.Lines
                .Where(line => !removedLineIds.Contains(line.VendorBillOcrDraftLineId))
                .ToList();
        }

        if (request.Lines is not null)
        {
            var lineLookup = draft.Lines.ToDictionary(line => line.VendorBillOcrDraftLineId);
            foreach (var lineRequest in request.Lines)
            {
                if (!lineLookup.TryGetValue(lineRequest.VendorBillOcrDraftLineId, out var line))
                {
                    throw new InvalidOperationException("Vendor bill OCR draft line was not found.");
                }

                ValidateLineRequest(lineRequest);
                line.ReviewedDescription = NormalizeOptionalText(lineRequest.ReviewedDescription, 300);
                line.ReviewedQuantity = lineRequest.ReviewedQuantity;
                line.ReviewedUnitCost = lineRequest.ReviewedUnitCost;
                line.ReviewedLineTotal = lineRequest.ReviewedLineTotal;
                line.SelectedInventoryItemId = lineRequest.SelectedInventoryItemId;
                line.IsIgnored = lineRequest.IsIgnored;
                line.UpdatedAtUtc = now;
            }
        }

        if (request.AddedLines is not null)
        {
            var nextLineNumber = draft.Lines.Count == 0 ? 1 : draft.Lines.Max(line => line.LineNumber) + 1;

            foreach (var lineRequest in request.AddedLines)
            {
                ValidateLineValues(lineRequest.ReviewedQuantity, lineRequest.ReviewedUnitCost, lineRequest.ReviewedLineTotal);
                var description = NormalizeOptionalText(lineRequest.ReviewedDescription, 300);
                if (string.IsNullOrWhiteSpace(description))
                {
                    throw new InvalidOperationException("Vendor bill OCR draft line description is required.");
                }

                draft.Lines.Add(new VendorBillOcrDraftLine
                {
                    RestaurantId = draft.RestaurantId,
                    BranchId = draft.BranchId,
                    VendorBillOcrDraftId = draft.VendorBillOcrDraftId,
                    LineNumber = nextLineNumber++,
                    ExtractedDescription = description,
                    ExtractedQuantity = lineRequest.ReviewedQuantity,
                    ExtractedUnitCost = lineRequest.ReviewedUnitCost,
                    ExtractedLineTotal = lineRequest.ReviewedLineTotal,
                    SelectedInventoryItemId = lineRequest.SelectedInventoryItemId,
                    IsIgnored = lineRequest.IsIgnored,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "VendorBillOcrDraft.Updated",
            reason: "OCR draft reviewed and corrected.",
            entityType: "VendorBillOcrDraft",
            entityId: draft.VendorBillOcrDraftId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(await ToDetailAsync(currentUser, draft, cancellationToken)),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToDetailAsync(currentUser, draft, cancellationToken);
    }

    public async Task<VendorBillDetail> ConfirmDraftAsync(AuthUserContext currentUser, Guid draftId, CancellationToken cancellationToken)
    {
        var restaurantId = RequireRestaurantScope(currentUser);
        var draft = await LoadTrackedDraftAsync(restaurantId, draftId, cancellationToken);
        EnsureDraftEditable(draft);
        EnsureBranchAccess(currentUser, draft.BranchId);

        if (draft.Status == VendorBillOcrDraftStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled OCR drafts cannot be confirmed.");
        }

        var vendorId = draft.ReviewedVendorId ?? throw new InvalidOperationException("Reviewed vendor is required before confirmation.");
        var vendor = await _context.Vendors
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.VendorId == vendorId, cancellationToken);

        if (vendor is null)
        {
            throw new KeyNotFoundException("Vendor not found.");
        }

        if (!vendor.IsActive)
        {
            throw new InvalidOperationException("Inactive vendors cannot receive new bills.");
        }

        if (vendor.BranchId.HasValue && vendor.BranchId.Value != draft.BranchId)
        {
            throw new InvalidOperationException("Vendor is not available for the selected branch.");
        }

        var billDate = draft.ReviewedBillDate ?? draft.ExtractedBillDate ?? DateTime.UtcNow.Date;
        var billNumber = draft.ReviewedBillNumber ?? draft.ExtractedBillNumber;
        var duplicateReceiptInfo = await GetDuplicateReceiptInfoAsync(
            restaurantId,
            draft.BranchId,
            vendorId,
            billNumber,
            billDate,
            draft.ReviewedTotalAmount ?? draft.ExtractedTotalAmount,
            cancellationToken);

        if (duplicateReceiptInfo.HasDuplicateReceipt && !HasPermission(currentUser, SystemPermissions.VendorBillOverrideOcr))
        {
            throw new InvalidOperationException("A matching vendor bill already exists. Use the override OCR permission to confirm anyway.");
        }

        var lines = draft.Lines
            .OrderBy(entity => entity.LineNumber)
            .Select(line =>
            {
                var quantity = line.ReviewedQuantity ?? line.ExtractedQuantity;
                var unitCost = line.ReviewedUnitCost ?? line.ExtractedUnitCost;
                var description = line.ReviewedDescription ?? line.ExtractedDescription;
                var lineTotal = line.ReviewedLineTotal ?? line.ExtractedLineTotal ?? (quantity.HasValue && unitCost.HasValue ? quantity.Value * unitCost.Value : null);

                if (quantity is null || unitCost is null)
                {
                    throw new InvalidOperationException("Vendor bill OCR draft line requires quantity and unit cost before confirmation.");
                }

                if (!line.IsIgnored && !line.SelectedInventoryItemId.HasValue)
                {
                    throw new InvalidOperationException("Each stock line must be mapped to an inventory item or marked ignored before confirmation.");
                }

                ValidateLineValues(quantity.Value, unitCost.Value, lineTotal);

                return new CreateVendorBillLineRequest(
                    line.IsIgnored ? null : line.SelectedInventoryItemId,
                    description,
                    quantity.Value,
                    unitCost.Value);
            })
            .ToArray();

        var totalAmount = RoundMoney(lines.Sum(line => line.Quantity * line.UnitCost));
        if (draft.ReviewedTotalAmount.HasValue && RoundMoney(draft.ReviewedTotalAmount.Value) != totalAmount)
        {
            throw new InvalidOperationException("Reviewed total amount must equal the sum of the vendor bill lines.");
        }

        var createRequest = new CreateVendorBillRequest(
            vendor.VendorId,
            draft.BranchId,
            billNumber,
            billDate,
            null,
            null,
            lines);

        var bill = await _vendorService.CreateVendorBillAsync(currentUser, createRequest, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, draft.BranchId, requireActive: false, cancellationToken);

        draft.Status = VendorBillOcrDraftStatus.Confirmed;
        draft.ConfirmedVendorBillId = bill.VendorBillId;
        draft.ConfirmedByUserId = RequireUserId(currentUser);
        draft.ConfirmedAtUtc = now;
        draft.UpdatedAtUtc = now;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "VendorBillOcrDraft.Confirmed",
            reason: "OCR draft confirmed into a vendor bill.",
            entityType: "VendorBillOcrDraft",
            entityId: draft.VendorBillOcrDraftId.ToString(),
            oldValueJson: null,
            newValueJson: Serialize(new
            {
                draft.VendorBillOcrDraftId,
                draft.ConfirmedVendorBillId,
                draft.ConfirmedAtUtc,
                draft.Status
            }),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return bill;
    }

    public async Task<VendorBillOcrDraftDetail> CancelDraftAsync(
        AuthUserContext currentUser,
        Guid draftId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Cancellation reason is required.");
        }

        var restaurantId = RequireRestaurantScope(currentUser);
        var draft = await LoadTrackedDraftAsync(restaurantId, draftId, cancellationToken);
        EnsureBranchAccess(currentUser, draft.BranchId);

        if (draft.Status == VendorBillOcrDraftStatus.Confirmed)
        {
            throw new InvalidOperationException("Confirmed OCR drafts cannot be cancelled.");
        }

        var restaurant = await LoadRestaurantAsync(restaurantId, cancellationToken);
        var branch = await LoadBranchAsync(restaurantId, draft.BranchId, requireActive: false, cancellationToken);
        var before = await ToDetailAsync(currentUser, draft, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        draft.Status = VendorBillOcrDraftStatus.Cancelled;
        draft.SafeErrorMessage = NormalizeOptionalText(reason, 500);
        draft.UpdatedAtUtc = now;

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        AddAudit(
            actor: currentUser,
            restaurant: restaurant,
            branch: branch,
            action: "VendorBillOcrDraft.Cancelled",
            reason: draft.SafeErrorMessage ?? "OCR draft cancelled.",
            entityType: "VendorBillOcrDraft",
            entityId: draft.VendorBillOcrDraftId.ToString(),
            oldValueJson: Serialize(before),
            newValueJson: Serialize(await ToDetailAsync(currentUser, draft, cancellationToken)),
            createdAt: now);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ToDetailAsync(currentUser, draft, cancellationToken);
    }

    private async Task<VendorBillOcrDraft> LoadDraftAsync(Guid restaurantId, Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await _context.VendorBillOcrDrafts
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.VendorBillOcrDraftId == draftId, cancellationToken);

        if (draft is null)
        {
            throw new KeyNotFoundException("Vendor bill OCR draft not found.");
        }

        return draft;
    }

    private async Task<VendorBillOcrDraft> LoadTrackedDraftAsync(Guid restaurantId, Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await _context.VendorBillOcrDrafts
            .Include(entity => entity.Lines)
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId && entity.VendorBillOcrDraftId == draftId, cancellationToken);

        if (draft is null)
        {
            throw new KeyNotFoundException("Vendor bill OCR draft not found.");
        }

        return draft;
    }

    private async Task<VendorBillOcrDraftDetail> ToDetailAsync(AuthUserContext currentUser, VendorBillOcrDraft draft, CancellationToken cancellationToken)
    {
        var duplicateReceiptInfo = await GetDuplicateReceiptInfoAsync(
            draft.RestaurantId,
            draft.BranchId,
            draft.ReviewedVendorId,
            draft.ReviewedBillNumber ?? draft.ExtractedBillNumber,
            draft.ReviewedBillDate ?? draft.ExtractedBillDate,
            draft.ReviewedTotalAmount ?? draft.ExtractedTotalAmount,
            cancellationToken);

        return new VendorBillOcrDraftDetail(
            draft.VendorBillOcrDraftId,
            draft.RestaurantId,
            draft.BranchId,
            draft.UploadedByUserId,
            draft.OriginalFileName,
            draft.ContentType,
            draft.FileSizeBytes,
            draft.Status.ToString(),
            draft.ExtractedVendorName,
            draft.ExtractedBillNumber,
            draft.ExtractedBillDate,
            draft.ExtractedTotalAmount,
            draft.ExtractedConfidenceScore,
            GetProviderWarnings(draft.ProviderWarningsJson),
            duplicateReceiptInfo.HasDuplicateReceipt,
            duplicateReceiptInfo.WarningMessage,
            HasPermission(currentUser, SystemPermissions.VendorBillOverrideOcr),
            draft.ReviewedVendorId,
            draft.ReviewedBillNumber,
            draft.ReviewedBillDate,
            draft.ReviewedTotalAmount,
            draft.SafeErrorMessage,
            draft.ConfirmedVendorBillId,
            draft.CreatedAtUtc,
            draft.UpdatedAtUtc,
            draft.ConfirmedAtUtc,
            draft.Lines
                .OrderBy(entity => entity.LineNumber)
                .Select(line => new VendorBillOcrDraftLineDetail(
                    line.VendorBillOcrDraftLineId,
                    line.LineNumber,
                    line.ExtractedDescription,
                    line.ExtractedQuantity,
                    line.ExtractedUnitCost,
                    line.ExtractedLineTotal,
                    line.ConfidenceScore,
                    line.SelectedInventoryItemId,
                    line.IsIgnored,
                    line.ReviewedDescription,
                    line.ReviewedQuantity,
                    line.ReviewedUnitCost,
                    line.ReviewedLineTotal,
                    line.CreatedAtUtc,
                    line.UpdatedAtUtc))
                .ToArray());
    }

    private static VendorBillOcrDraftListItem ToListItem(VendorBillOcrDraft draft)
    {
        return new VendorBillOcrDraftListItem(
            draft.VendorBillOcrDraftId,
            draft.RestaurantId,
            draft.BranchId,
            draft.OriginalFileName,
            draft.Status.ToString(),
            draft.CreatedAtUtc,
            draft.UpdatedAtUtc);
    }

    private void ApplyExtraction(
        VendorBillOcrDraft draft,
        VendorBillOcrProviderResult providerResult,
        Guid restaurantId,
        Guid branchId,
        DateTimeOffset now)
    {
        var extraction = providerResult.Extraction!;
        draft.ExtractedVendorName = extraction.VendorName?.Value;
        draft.ExtractedBillNumber = extraction.BillNumber?.Value;
        draft.ExtractedBillDate = extraction.BillDate?.Value;
        draft.ExtractedTotalAmount = extraction.TotalAmount?.Value;
        draft.ExtractedConfidenceScore = providerResult.OverallConfidence;
        draft.ProviderWarningsJson = providerResult.Warnings.Count == 0 ? null : Serialize(providerResult.Warnings);
        draft.Lines = extraction.Lines.Select((line, index) =>
        {
            var description = line.Description?.Value;
            var quantity = line.Quantity?.Value;
            var unitCost = line.UnitCost?.Value;
            var lineTotal = line.LineTotal?.Value;
            var lineConfidence = CalculateLineConfidence(line);

            return new VendorBillOcrDraftLine
            {
                RestaurantId = restaurantId,
                BranchId = branchId,
                VendorBillOcrDraftId = draft.VendorBillOcrDraftId,
                LineNumber = index + 1,
                ExtractedDescription = description ?? string.Empty,
                ExtractedQuantity = quantity,
                ExtractedUnitCost = unitCost,
                ExtractedLineTotal = lineTotal,
                ConfidenceScore = lineConfidence,
                IsIgnored = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }).ToList();
        draft.Status = VendorBillOcrDraftStatus.Extracted;
        draft.SafeErrorMessage = null;
    }

    private static void ValidateRequest(UpdateVendorBillOcrDraftRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Request is required.");
        }
    }

    private static void ValidateLineRequest(UpdateVendorBillOcrDraftLineRequest request)
    {
        if (request.ReviewedQuantity.HasValue && request.ReviewedQuantity.Value <= 0m)
        {
            throw new InvalidOperationException("Vendor bill OCR draft line quantity must be greater than zero.");
        }

        if (request.ReviewedUnitCost.HasValue && request.ReviewedUnitCost.Value < 0m)
        {
            throw new InvalidOperationException("Vendor bill OCR draft line unit cost must be zero or greater.");
        }
    }

    private async Task<DuplicateReceiptInfo> GetDuplicateReceiptInfoAsync(
        Guid restaurantId,
        Guid branchId,
        Guid? vendorId,
        string? billNumber,
        DateTime? billDate,
        decimal? totalAmount,
        CancellationToken cancellationToken)
    {
        if (!vendorId.HasValue || string.IsNullOrWhiteSpace(billNumber) || !billDate.HasValue || !totalAmount.HasValue)
        {
            return DuplicateReceiptInfo.None;
        }

        var normalizedBillNumber = NormalizeOptionalText(billNumber, 40);
        if (string.IsNullOrWhiteSpace(normalizedBillNumber))
        {
            return DuplicateReceiptInfo.None;
        }

        var candidateBills = await _context.VendorBills
            .AsNoTracking()
            .Where(entity => entity.RestaurantId == restaurantId)
            .Where(entity => entity.BranchId == branchId)
            .Where(entity => entity.VendorId == vendorId.Value)
            .Where(entity => entity.BillDate == billDate.Value.Date)
            .Where(entity => entity.Status != VendorBillStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var duplicateCount = candidateBills.Count(entity =>
            string.Equals(NormalizeOptionalText(entity.BillNumber, 40), normalizedBillNumber, StringComparison.OrdinalIgnoreCase) &&
            RoundMoney(entity.TotalAmount) == RoundMoney(totalAmount.Value));

        if (duplicateCount == 0)
        {
            return DuplicateReceiptInfo.None;
        }

        var warningMessage = duplicateCount == 1
            ? "A matching vendor bill already exists for this receipt."
            : $"There are {duplicateCount} matching vendor bills for this receipt.";

        return new DuplicateReceiptInfo(true, warningMessage, false);
    }

    private static void ValidateLineValues(decimal quantity, decimal unitCost, decimal? lineTotal)
    {
        if (quantity <= 0m)
        {
            throw new InvalidOperationException("Vendor bill OCR draft line quantity must be greater than zero.");
        }

        if (unitCost < 0m)
        {
            throw new InvalidOperationException("Vendor bill OCR draft line unit cost must be zero or greater.");
        }

        if (lineTotal.HasValue && lineTotal.Value < 0m)
        {
            throw new InvalidOperationException("Vendor bill OCR draft line total must be zero or greater.");
        }
    }

    private static void EnsureDraftEditable(VendorBillOcrDraft draft)
    {
        if (draft.Status == VendorBillOcrDraftStatus.Confirmed)
        {
            throw new InvalidOperationException("Confirmed OCR drafts cannot be edited.");
        }
    }

    private static Guid RequireRestaurantScope(AuthUserContext currentUser)
    {
        if (currentUser.RestaurantId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the restaurant scope.");
        }

        return currentUser.RestaurantId;
    }

    private static Guid RequireUserId(AuthUserContext currentUser)
    {
        if (currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authentication context is missing the user scope.");
        }

        return currentUser.UserId;
    }

    private static bool HasPermission(AuthUserContext currentUser, string permissionCode) =>
        currentUser.Permissions.Any(permission => string.Equals(permission, permissionCode, StringComparison.OrdinalIgnoreCase));

    private static Guid? ResolveBranchFilter(AuthUserContext currentUser, Guid? requestedBranchId)
    {
        if (currentUser.BranchId.HasValue)
        {
            if (requestedBranchId.HasValue && requestedBranchId.Value != currentUser.BranchId.Value)
            {
                throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
            }

            return currentUser.BranchId.Value;
        }

        return requestedBranchId;
    }

    private static Guid ResolveBranchScope(AuthUserContext currentUser, Guid? requestedBranchId)
    {
        if (currentUser.BranchId.HasValue)
        {
            if (requestedBranchId.HasValue && requestedBranchId.Value != currentUser.BranchId.Value)
            {
                throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
            }

            return currentUser.BranchId.Value;
        }

        if (!requestedBranchId.HasValue)
        {
            throw new InvalidOperationException("Branch is required.");
        }

        return requestedBranchId.Value;
    }

    private static void EnsureBranchAccess(AuthUserContext currentUser, Guid branchId)
    {
        if (currentUser.BranchId.HasValue && currentUser.BranchId.Value != branchId)
        {
            throw new UnauthorizedAccessException("Branch access is restricted to the current branch.");
        }
    }

    private async Task<Branch> LoadBranchAsync(Guid restaurantId, Guid branchId, bool requireActive, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.BranchId == branchId && entity.RestaurantId == restaurantId, cancellationToken);

        if (branch is null)
        {
            throw new KeyNotFoundException("Branch not found.");
        }

        if (requireActive && branch.Status != BranchStatus.Active)
        {
            throw new InvalidOperationException("Branch must be active within the current restaurant.");
        }

        return branch;
    }

    private async Task<Restaurant> LoadRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var restaurant = await _context.Restaurants
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.RestaurantId == restaurantId, cancellationToken);

        if (restaurant is null)
        {
            throw new KeyNotFoundException("Restaurant not found.");
        }

        return restaurant;
    }

    private sealed record DuplicateReceiptInfo(bool HasDuplicateReceipt, string? WarningMessage, bool CanOverrideDuplicateReceipt)
    {
        public static DuplicateReceiptInfo None { get; } = new(false, null, false);
    }

    private void AddAudit(
        AuthUserContext actor,
        Restaurant restaurant,
        Branch? branch,
        string action,
        string reason,
        string entityType,
        string entityId,
        string? oldValueJson,
        string? newValueJson,
        DateTimeOffset createdAt)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            RestaurantId = restaurant.RestaurantId,
            BranchId = branch?.BranchId,
            UserId = actor.UserId == Guid.Empty ? null : actor.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Reason = reason,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            RestaurantNameSnapshot = restaurant.Name,
            BranchNameSnapshot = branch?.Name,
            UserNameSnapshot = actor.FullName,
            UserMobileSnapshot = actor.MobileNumber,
            CreatedAt = createdAt
        });
    }

    private static string Serialize(object? value) => value is null ? string.Empty : JsonSerializer.Serialize(value);

    private static IReadOnlyCollection<string> GetProviderWarnings(string? warningsJson)
    {
        if (string.IsNullOrWhiteSpace(warningsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(warningsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal? CalculateLineConfidence(VendorBillOcrLineExtraction line)
    {
        var values = new[]
        {
            line.Description?.ConfidenceScore,
            line.Quantity?.ConfidenceScore,
            line.UnitCost?.ConfidenceScore,
            line.LineTotal?.ConfidenceScore
        };

        var numericValues = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return numericValues.Length == 0 ? null : numericValues.Min();
    }

    private static VendorBillOcrProviderResult CreateProviderFailure((string ErrorCode, string ErrorMessage) failure)
    {
        return new VendorBillOcrProviderResult(
            false,
            failure.ErrorCode,
            failure.ErrorMessage,
            null,
            null,
            Array.Empty<string>(),
            null);
    }

    private static (string ErrorCode, string ErrorMessage) MapProviderException(Exception exception)
    {
        return exception switch
        {
            InvalidDataException => ("InvalidDocument", "OCR could not extract useful data."),
            FormatException => ("InvalidDocument", "OCR could not extract useful data."),
            TimeoutException => ("TransientFailure", "OCR service is temporarily unavailable."),
            IOException => ("TransientFailure", "OCR service is temporarily unavailable."),
            _ => ("UnknownProviderFailure", "OCR service is temporarily unavailable.")
        };
    }
}
