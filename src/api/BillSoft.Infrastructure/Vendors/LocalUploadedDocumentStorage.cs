using System.Security.Cryptography;
using BillSoft.Application.Vendors;
using Microsoft.Extensions.Options;

namespace BillSoft.Infrastructure.Vendors;

public sealed class LocalUploadedDocumentStorage : IUploadedDocumentStorage
{
    private readonly string _rootPath;

    public LocalUploadedDocumentStorage(IOptions<OcrOptions> options)
    {
        var configuredRootPath = options?.Value.StorageRootPath;
        _rootPath = string.IsNullOrWhiteSpace(configuredRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "vendor-bill-ocr")
            : configuredRootPath;
    }

    public async Task<StoredUploadedDocument> SaveAsync(
        string originalFileName,
        string contentType,
        Stream fileContent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(fileContent);

        Directory.CreateDirectory(_rootPath);

        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine("vendor-bill-ocr", storedFileName);
        var absolutePath = Path.Combine(_rootPath, storedFileName);

        await using var output = File.Create(absolutePath);
        await fileContent.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);

        var size = output.Length;

        return new StoredUploadedDocument(relativePath, originalFileName, contentType, size);
    }
}
