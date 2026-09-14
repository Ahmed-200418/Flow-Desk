namespace FlowDesk.Application.Common.Interfaces;

public interface IAttachmentStorageService
{
    Task<(string StoredFileName, string FilePath)> SaveFileAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default);
    Task<Stream> GetFileStreamAsync(string filePath, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);
    bool IsAllowedExtension(string fileName);
}
