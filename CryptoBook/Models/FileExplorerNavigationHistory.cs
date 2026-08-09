using CryptoBook.DTO;

using System.IO;

namespace CryptoBook.Models
{
    internal sealed class FileExplorerNavigationHistory
    {
        private readonly List<string> _backPaths = [];
        private readonly List<string> _forwardPaths = [];

        public bool CanGoBack => _backPaths.Count > 0;
        public bool CanGoForward => _forwardPaths.Count > 0;
        public string? BackPath => _backPaths.LastOrDefault();
        public string? ForwardPath => _forwardPaths.LastOrDefault();

        internal IReadOnlyList<string> BackPaths => _backPaths;
        internal IReadOnlyList<string> ForwardPaths => _forwardPaths;

        public void Commit(
            string? previousPath,
            string targetPath,
            FileExplorerNavigationMode mode)
        {
            string target = Normalize(targetPath);
            string? previous = string.IsNullOrWhiteSpace(previousPath)
                ? null
                : Normalize(previousPath);

            switch(mode)
            {
                case FileExplorerNavigationMode.Standard:
                    if(previous is not null && PathsEqual(previous, target))
                        break;
                    if(previous is not null)
                        PushDistinct(_backPaths, previous);
                    _forwardPaths.Clear();
                    break;

                case FileExplorerNavigationMode.Back:
                    RemoveTarget(_backPaths, target);
                    if(previous is not null && !PathsEqual(previous, target))
                        PushDistinct(_forwardPaths, previous);
                    break;

                case FileExplorerNavigationMode.Forward:
                    RemoveTarget(_forwardPaths, target);
                    if(previous is not null && !PathsEqual(previous, target))
                        PushDistinct(_backPaths, previous);
                    break;

                case FileExplorerNavigationMode.Restore:
                    _backPaths.Clear();
                    _forwardPaths.Clear();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private static void RemoveTarget(List<string> paths, string target)
        {
            if(paths.Count > 0 && PathsEqual(paths[^1], target))
                paths.RemoveAt(paths.Count - 1);
        }

        private static void PushDistinct(List<string> paths, string path)
        {
            if(paths.Count == 0 || !PathsEqual(paths[^1], path))
                paths.Add(path);
        }

        private static string Normalize(string path) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        private static bool PathsEqual(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
