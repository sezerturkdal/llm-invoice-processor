using InvoiceProcessor.Core.Files;

namespace InvoiceProcessor.Infrastructure.Storage;

/// <summary>Stores files on the local disk (a Docker volume in containers) as {yyyy}/{MM}/{guid}{ext}.</summary>
public sealed class LocalFileStorage(string rootPath, TimeProvider timeProvider) : IFileStorage
{
    private const int BufferSize = 81920;

    private readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var path = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var fullPath = Resolve(path);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            await content.CopyToAsync(file, cancellationToken);
        }
        catch
        {
            // Don't leave a partial file behind, e.g. when the client disconnects mid-upload.
            File.Delete(fullPath);
            throw;
        }

        return path;
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Stored file '{path}' was not found.", path);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        File.Delete(Resolve(path));
        return Task.CompletedTask;
    }

    // Paths come from our own database, but never let one escape the storage root.
    private string Resolve(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, path));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Path '{path}' is outside the storage root.", nameof(path));
        }

        return fullPath;
    }
}
