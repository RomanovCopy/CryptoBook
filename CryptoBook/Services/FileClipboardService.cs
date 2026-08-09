using CryptoBook.DTO;
using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    /// <summary>
    /// Хранит только намерение Copy/Move; файловую операцию выполняет общий координатор.
    /// </summary>
    public sealed class FileClipboardService: IFileClipboardService
    {
        private readonly object _sync = new();
        private ClipboardData _clipboard = new();

        public void SetCopy(IEnumerable<string> sourcePaths) =>
            Set(sourcePaths, ClipboardOperationKind.Copy);

        public void SetMove(IEnumerable<string> sourcePaths) =>
            Set(sourcePaths, ClipboardOperationKind.Move);

        public ClipboardData GetData()
        {
            lock(_sync)
                return _clipboard;
        }

        public void Clear()
        {
            lock(_sync)
                _clipboard = new ClipboardData();
        }

        private void Set(
            IEnumerable<string> sourcePaths,
            ClipboardOperationKind operation)
        {
            ArgumentNullException.ThrowIfNull(sourcePaths);
            string[] paths = sourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            lock(_sync)
            {
                _clipboard = new ClipboardData
                {
                    SourcePaths = paths,
                    Operation = operation,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }
    }
}
