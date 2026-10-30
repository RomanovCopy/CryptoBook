using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.IO;

namespace CryptoBook.Validation;

public sealed class ValidationRecycleBin(string root) : IRecycleBinService
{
    public string? LastRecycledPath { get; private set; }
    public async Task<FileOperationResult> RecycleAsync(string path, CancellationToken cancellationToken = default)
    {
        if(!Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Recycle validation path escaped its disposable root.");
        var result = await new WindowsRecycleBinService().RecycleAsync(path, cancellationToken);
        if(result.Success) LastRecycledPath = result.AffectedPath;
        return result;
    }
}
