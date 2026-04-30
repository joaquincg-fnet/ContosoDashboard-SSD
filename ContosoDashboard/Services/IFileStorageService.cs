namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a stream to the storage backend at the given relative path.
    /// Creates any required directory structure automatically.
    /// </summary>
    Task SaveAsync(Stream fileStream, string relativePath);

    /// <summary>
    /// Opens a read stream for a file at the given relative path.
    /// Throws FileNotFoundException if the file does not exist.
    /// </summary>
    Task<Stream> OpenReadAsync(string relativePath);

    /// <summary>
    /// Returns the absolute physical path for a relative path.
    /// Validates that the resolved path is within the storage root (path traversal guard).
    /// Throws InvalidOperationException if the path escapes the storage root.
    /// </summary>
    string ResolveAbsolutePath(string relativePath);

    /// <summary>
    /// Deletes a file at the given relative path.
    /// No-op if the file does not exist.
    /// </summary>
    Task DeleteAsync(string relativePath);
}
