using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Application.Common.Interfaces;

namespace FlowDesk.Infrastructure.Services;

public class AttachmentStorageService : IAttachmentStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".docx", ".xlsx", ".pptx", ".txt", ".csv", ".zip"
    };

    private readonly string _storageFolder;

    public AttachmentStorageService()
    {
        _storageFolder = Path.Combine(Directory.GetCurrentDirectory(), "storage", "attachments");
        if (!Directory.Exists(_storageFolder))
        {
            Directory.CreateDirectory(_storageFolder);
        }
    }

    public bool IsAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
    }

    public async Task<(string StoredFileName, string FilePath)> SaveFileAsync(Stream fileStream, string originalFileName, CancellationToken cancellationToken = default)
    {
        if (!IsAllowedExtension(originalFileName))
        {
            throw new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("File", $"File extension '{Path.GetExtension(originalFileName)}' is not allowed for security reasons.")
            });
        }

        var ext = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(_storageFolder, storedFileName);

        using (var destinationStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(destinationStream, cancellationToken);
        }

        return (storedFileName, filePath);
    }

    public Task<Stream> GetFileStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new NotFoundException("Attachment file was not found on physical storage.");
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}
