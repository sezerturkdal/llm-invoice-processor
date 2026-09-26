namespace InvoiceProcessor.Core.Files;

/// <summary>Stores uploaded invoice documents. Paths are opaque keys relative to the storage root.</summary>
public interface IFileStorage
{
    /// <summary>Saves the content under a new unique path and returns that path.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    /// <exception cref="FileNotFoundException">No file exists at <paramref name="path"/>.</exception>
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
