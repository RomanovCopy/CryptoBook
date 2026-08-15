using System.IO;

namespace CryptoBook.Infrastructure;

/// <summary>
/// Открывает файл для чтения, не запрещая его атомарную замену,
/// перемещение или удаление другим файловым workflow.
/// </summary>
internal static class SharedFileReadStream
{
    internal const FileShare ShareMode = FileShare.Read | FileShare.Delete;

    internal static FileStream Open(
        string path,
        int bufferSize,
        bool asynchronous = true) =>
        new(
            path,
            FileMode.Open,
            FileAccess.Read,
            ShareMode,
            bufferSize,
            asynchronous
                ? FileOptions.Asynchronous | FileOptions.SequentialScan
                : FileOptions.SequentialScan);
}
