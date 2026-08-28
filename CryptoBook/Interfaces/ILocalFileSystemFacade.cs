namespace CryptoBook.Interfaces;

/// <summary>
/// Local-only path workspace for features that have not yet been made
/// provider-neutral (encryption export and temporary files).
/// </summary>
public interface ILocalFileSystemFacade: IService
{
    string? GetParent(string path);
    string Combine(params string[] parts);
    string GetName(string path);
    string GetNameWithoutExtension(string path);
    string GetExtension(string path);
    string ChangeExtension(string path, string? extension);
    string Normalize(string path);
    string TemporaryPath(params string[] parts);
    bool Exists(string path);
    bool FileExists(string path);
    bool IsValidFileName(string name);
    void EnsureDirectory(string path);
    IReadOnlyList<string> EnumerateDirectories(string path);
    void DeleteDirectoryIfExists(string path);
    void DeleteFileIfExists(string path);
    Task CopyFileAtomicallyAsync(
        string sourcePath,
        string destinationPath,
        IProgressReporter? progress = null,
        CancellationToken cancellationToken = default);
}
