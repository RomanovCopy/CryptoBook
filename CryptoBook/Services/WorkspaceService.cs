using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services
{
    public sealed class WorkspaceService: IWorkspaceService
    {
        private const string LocalScheme = "local://";

        public string WorkspaceDirectory
        {
            get
            {
                string storedPath =
                    Properties.Settings.Default.WorkspaceDirectory ??
                    string.Empty;
                return string.IsNullOrWhiteSpace(storedPath)
                    ? string.Empty
                    : GetNativeLocalPath(storedPath);
            }
        }

        public void SetWorkspaceDirectory(string path)
        {
            string nativePath = GetNativeLocalPath(path);
            if(!Directory.Exists(nativePath))
                throw new DirectoryNotFoundException(
                    $"Рабочая директория не найдена: {nativePath}");

            Properties.Settings.Default.WorkspaceDirectory =
                LocalScheme + nativePath;
            Properties.Settings.Default.Save();
        }

        public Task<WorkspaceSearchOutcome> SearchFilesAsync(
            string query,
            int maxResults = 200,
            CancellationToken cancellationToken = default)
        {
            string root = GetNativeLocalPath(
                Properties.Settings.Default.WorkspaceDirectory);
            string normalizedQuery = query?.Trim() ?? string.Empty;

            if(normalizedQuery.Length == 0)
                throw new ArgumentException(
                    "Введите часть имени файла.",
                    nameof(query));
            if(maxResults <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxResults));
            if(!Directory.Exists(root))
                throw new DirectoryNotFoundException(
                    $"Рабочая директория не найдена: {root}");

            return Task.Run(
                () => SearchCore(
                    root,
                    normalizedQuery,
                    maxResults,
                    cancellationToken),
                cancellationToken);
        }

        private static WorkspaceSearchOutcome SearchCore(
            string root,
            string query,
            int maxResults,
            CancellationToken cancellationToken)
        {
            var results = new List<WorkspaceSearchResult>();
            var pendingDirectories = new Stack<string>();
            int skippedDirectoryCount = 0;
            bool isTruncated = false;

            pendingDirectories.Push(root);

            // Обходим дерево вручную: недоступная подпапка не прерывает весь поиск.
            while(pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pendingDirectories.Pop();

                try
                {
                    foreach(string filePath in Directory.EnumerateFiles(directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string name = Path.GetFileName(filePath);
                        if(!name.Contains(
                            query,
                            StringComparison.CurrentCultureIgnoreCase))
                        {
                            continue;
                        }

                        if(results.Count == maxResults)
                        {
                            isTruncated = true;
                            return CreateOutcome();
                        }

                        results.Add(new WorkspaceSearchResult(
                            name,
                            filePath,
                            Path.GetRelativePath(root, filePath)));
                    }

                    foreach(string childDirectory in
                        Directory.EnumerateDirectories(directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        FileAttributes attributes;
                        try
                        {
                            attributes = File.GetAttributes(childDirectory);
                        }
                        catch(UnauthorizedAccessException)
                        {
                            skippedDirectoryCount++;
                            continue;
                        }
                        catch(IOException)
                        {
                            skippedDirectoryCount++;
                            continue;
                        }

                        if((attributes & FileAttributes.ReparsePoint) == 0)
                            pendingDirectories.Push(childDirectory);
                    }
                }
                catch(UnauthorizedAccessException)
                {
                    skippedDirectoryCount++;
                }
                catch(IOException)
                {
                    skippedDirectoryCount++;
                }
            }

            return CreateOutcome();

            WorkspaceSearchOutcome CreateOutcome() =>
                new(
                    results
                        .OrderBy(result => result.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(result => result.RelativePath, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray(),
                    isTruncated,
                    skippedDirectoryCount);
        }

        private static string GetNativeLocalPath(string path)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException(
                    "Рабочая директория не выбрана.");

            string trimmedPath = path.Trim();
            int schemeSeparator = trimmedPath.IndexOf(
                "://",
                StringComparison.Ordinal);
            if(schemeSeparator < 0)
                return Path.GetFullPath(trimmedPath);

            string scheme = trimmedPath[..(schemeSeparator + 3)];
            if(!scheme.Equals(LocalScheme, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException(
                    "Поиск пока поддерживается только в локальной рабочей директории.");

            return Path.GetFullPath(trimmedPath[LocalScheme.Length..]);
        }
    }
}
