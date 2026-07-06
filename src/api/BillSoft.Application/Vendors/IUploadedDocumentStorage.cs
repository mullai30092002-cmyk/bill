namespace BillSoft.Application.Vendors;

public interface IUploadedDocumentStorage
{
    Task<StoredUploadedDocument> SaveAsync(
        string originalFileName,
        string contentType,
        Stream fileContent,
        CancellationToken cancellationToken);
}

public sealed record StoredUploadedDocument(
    string RelativePath,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);
