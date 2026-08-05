using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.Text;

namespace CryptoBook.Services
{
    /// <summary>
    /// Координирует обход рабочего пространства, доступ к защищённым
    /// документам и передачу содержимого подходящему extractor-у.
    /// </summary>
    public sealed class WorkspaceContentSearchService:
        IWorkspaceContentSearchService
    {
        private const long EncryptedContainerOverheadAllowance =
            2 * 1024 * 1024;

        private readonly IWorkspaceService workspaceService;
        private readonly ISecureFileValidator secureFileValidator;
        private readonly ISecureFileProcessor secureFileProcessor;
        private readonly IEncryptionKeyRequestService keyRequestService;
        private readonly IReadOnlyList<IDocumentTextExtractor> extractors;
        private readonly WorkspaceContentSearchOptions options;

        public WorkspaceContentSearchService(
            IWorkspaceService workspaceService,
            ISecureFileValidator secureFileValidator,
            ISecureFileProcessor secureFileProcessor,
            IEncryptionKeyRequestService keyRequestService,
            IEnumerable<IDocumentTextExtractor> extractors,
            WorkspaceContentSearchOptions options)
        {
            this.workspaceService = workspaceService ??
                throw new ArgumentNullException(nameof(workspaceService));
            this.secureFileValidator = secureFileValidator ??
                throw new ArgumentNullException(nameof(secureFileValidator));
            this.secureFileProcessor = secureFileProcessor ??
                throw new ArgumentNullException(nameof(secureFileProcessor));
            this.keyRequestService = keyRequestService ??
                throw new ArgumentNullException(nameof(keyRequestService));
            this.extractors = extractors?.ToArray() ??
                throw new ArgumentNullException(nameof(extractors));
            this.options = options ??
                throw new ArgumentNullException(nameof(options));

            if(options.MaxResults <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaxResults));
            if(options.MaxFileSizeBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.MaxFileSizeBytes));
            if(options.SnippetLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.SnippetLength));
        }

        public async Task<WorkspaceContentSearchOutcome> SearchAsync(
            string query,
            IProgress<WorkspaceContentSearchProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string normalizedQuery = query?.Trim() ?? string.Empty;
            if(normalizedQuery.Length == 0)
                throw new ArgumentException(
                    LocalizationManager.GetString(
                        "Workspace.ContentSearch.EnterQuery"),
                    nameof(query));

            string root = workspaceService.WorkspaceDirectory;
            if(string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException(
                    LocalizationManager.GetString("Workspace.NotSelected"));
            root = Path.GetFullPath(root);
            if(!Directory.Exists(root))
                throw new DirectoryNotFoundException(
                    LocalizationManager.Format(
                        "Workspace.DirectoryNotFound",
                        root));

            var results = new List<WorkspaceContentSearchResult>();
            var pendingDirectories = new Stack<string>();
            int processedFileCount = 0;
            int skippedDirectoryCount = 0;
            int skippedFileCount = 0;
            int skippedEncryptedFileCount = 0;
            bool encryptedAccessResolved = false;
            bool encryptedAccessAllowed = false;
            pendingDirectories.Push(root);

            while(pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pendingDirectories.Pop();

                string[] files;
                string[] childDirectories;
                try
                {
                    files = Directory.GetFiles(directory);
                    childDirectories = Directory.GetDirectories(directory);
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

                foreach(string filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    processedFileCount++;
                    string relativePath = Path.GetRelativePath(root, filePath);
                    progress?.Report(new WorkspaceContentSearchProgress(
                        processedFileCount,
                        relativePath));

                    try
                    {
                        bool isEncrypted = await secureFileValidator
                            .HasCryptoBookHeaderAsync(
                                filePath,
                                cancellationToken);
                        string extension = Path.GetExtension(filePath);
                        IDocumentTextExtractor? extractor = isEncrypted
                            ? null
                            : FindExtractor(extension);

                        if(!isEncrypted && extractor is null)
                            continue;

                        string text;
                        if(isEncrypted)
                        {
                            if(new FileInfo(filePath).Length >
                               options.MaxFileSizeBytes +
                               EncryptedContainerOverheadAllowance)
                            {
                                skippedFileCount++;
                                continue;
                            }

                            if(!encryptedAccessResolved)
                            {
                                encryptedAccessAllowed =
                                    keyRequestService.EnsureKeyAvailable();
                                encryptedAccessResolved = true;
                            }
                            if(!encryptedAccessAllowed)
                            {
                                skippedEncryptedFileCount++;
                                continue;
                            }

                            await using DecryptedFileContent decrypted =
                                await secureFileProcessor
                                    .DecryptFileContentAsync(
                                        filePath,
                                        cancellationToken:
                                            cancellationToken);
                            if(decrypted.Content.CanSeek &&
                               decrypted.Content.Length >
                               options.MaxFileSizeBytes)
                            {
                                skippedFileCount++;
                                continue;
                            }

                            extractor = FindExtractor(
                                decrypted.OriginalExtension);
                            if(extractor is null)
                                continue;
                            text = await extractor.ExtractAsync(
                                decrypted.Content,
                                decrypted.OriginalExtension,
                                cancellationToken);
                        }
                        else
                        {
                            if(new FileInfo(filePath).Length >
                               options.MaxFileSizeBytes)
                            {
                                skippedFileCount++;
                                continue;
                            }

                            await using FileStream stream = new(
                                filePath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.ReadWrite | FileShare.Delete,
                                81920,
                                FileOptions.Asynchronous |
                                FileOptions.SequentialScan);
                            text = await extractor!.ExtractAsync(
                                stream,
                                extension,
                                cancellationToken);
                        }

                        int firstMatch = text.IndexOf(
                            normalizedQuery,
                            StringComparison.CurrentCultureIgnoreCase);
                        if(firstMatch < 0)
                            continue;

                        results.Add(new WorkspaceContentSearchResult(
                            Path.GetFileName(filePath),
                            filePath,
                            relativePath,
                            CreateSnippet(
                                text,
                                firstMatch,
                                normalizedQuery.Length,
                                options.SnippetLength),
                            CountMatches(text, normalizedQuery),
                            isEncrypted));

                        if(results.Count >= options.MaxResults)
                        {
                            return CreateOutcome(isTruncated: true);
                        }
                    }
                    catch(OperationCanceledException)
                    {
                        throw;
                    }
                    catch(UnauthorizedAccessException)
                    {
                        skippedFileCount++;
                    }
                    catch(IOException)
                    {
                        skippedFileCount++;
                    }
                    catch(System.Security.Cryptography.CryptographicException)
                    {
                        skippedEncryptedFileCount++;
                    }
                    catch(NotSupportedException)
                    {
                        skippedFileCount++;
                    }
                    catch(ArgumentException)
                    {
                        skippedFileCount++;
                    }
                    catch(System.Xml.XmlException)
                    {
                        skippedFileCount++;
                    }
                    catch(System.Windows.Markup.XamlParseException)
                    {
                        skippedFileCount++;
                    }
                }

                foreach(string childDirectory in childDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        FileAttributes attributes =
                            File.GetAttributes(childDirectory);
                        if((attributes & FileAttributes.ReparsePoint) == 0)
                            pendingDirectories.Push(childDirectory);
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
            }

            return CreateOutcome(isTruncated: false);

            WorkspaceContentSearchOutcome CreateOutcome(bool isTruncated) =>
                new(
                    results
                        .OrderBy(
                            result => result.Name,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(
                            result => result.RelativePath,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray(),
                    isTruncated,
                    skippedDirectoryCount,
                    skippedFileCount,
                    skippedEncryptedFileCount);
        }

        private IDocumentTextExtractor? FindExtractor(string extension) =>
            extractors.FirstOrDefault(extractor =>
                extractor.CanExtract(extension));

        private static int CountMatches(string text, string query)
        {
            int count = 0;
            int offset = 0;
            while(offset <= text.Length - query.Length)
            {
                int index = text.IndexOf(
                    query,
                    offset,
                    StringComparison.CurrentCultureIgnoreCase);
                if(index < 0)
                    break;

                count++;
                offset = index + query.Length;
            }
            return count;
        }

        private static string CreateSnippet(
            string text,
            int matchIndex,
            int matchLength,
            int maximumLength)
        {
            int contextLength = Math.Max(0, maximumLength - matchLength);
            int start = Math.Max(0, matchIndex - contextLength / 2);
            int length = Math.Min(
                text.Length - start,
                Math.Max(matchLength, maximumLength));
            string normalized = NormalizeWhitespace(
                text.Substring(start, length));
            return (start > 0 ? "…" : string.Empty) +
                normalized +
                (start + length < text.Length ? "…" : string.Empty);
        }

        private static string NormalizeWhitespace(string value)
        {
            var result = new StringBuilder(value.Length);
            bool previousWasWhitespace = false;
            foreach(char character in value)
            {
                if(char.IsWhiteSpace(character))
                {
                    if(!previousWasWhitespace)
                        result.Append(' ');
                    previousWasWhitespace = true;
                }
                else
                {
                    result.Append(character);
                    previousWasWhitespace = false;
                }
            }
            return result.ToString().Trim();
        }
    }
}
