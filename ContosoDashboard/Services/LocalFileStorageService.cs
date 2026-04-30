namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageRoot;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        // Store files in AppData/uploads/ alongside the app — NOT inside wwwroot
        _storageRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "AppData", "uploads"));
    }

    public async Task SaveAsync(Stream fileStream, string relativePath)
    {
        var absPath = ResolveAbsolutePath(relativePath);

        var directory = Path.GetDirectoryName(absPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var fs = new FileStream(absPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(fs);
    }

    public Task<Stream> OpenReadAsync(string relativePath)
    {
        var absPath = ResolveAbsolutePath(relativePath);
        if (!File.Exists(absPath))
            throw new FileNotFoundException($"File not found: {relativePath}", absPath);

        Stream stream = new FileStream(absPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public string ResolveAbsolutePath(string relativePath)
    {
        // Normalise: reject obviously dangerous patterns early
        if (relativePath.Contains(".."))
            throw new InvalidOperationException("Path traversal detected.");

        var resolved = Path.GetFullPath(Path.Combine(_storageRoot, relativePath));

        // Guard: resolved path must be inside the storage root
        if (!resolved.StartsWith(_storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !resolved.Equals(_storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path traversal detected.");
        }

        return resolved;
    }

    public Task DeleteAsync(string relativePath)
    {
        try
        {
            var absPath = ResolveAbsolutePath(relativePath);
            if (File.Exists(absPath))
                File.Delete(absPath);
        }
        catch (InvalidOperationException)
        {
            // Path traversal attempt — do nothing
        }
        return Task.CompletedTask;
    }
}
