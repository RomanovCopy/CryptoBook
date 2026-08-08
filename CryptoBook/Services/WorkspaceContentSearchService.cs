using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System.IO;
using System.Text;

namespace CryptoBook.Services;

/// <summary>
/// Ищет открытые документы через инкрементальный индекс, а защищённые — только
/// в памяти после явного доступа к ключу.
/// </summary>
public sealed class WorkspaceContentSearchService: IWorkspaceContentSearchService
{
    private readonly IWorkspaceService workspaceService;
    private readonly IWorkspaceSearchIndex searchIndex;
    private readonly ISecureFileProcessor secureFileProcessor;
    private readonly IEncryptionKeyRequestService keyRequestService;

    public WorkspaceContentSearchService(
        IWorkspaceService workspaceService,
        IWorkspaceSearchIndex searchIndex,
        ISecureFileProcessor secureFileProcessor,
        IEncryptionKeyRequestService keyRequestService)
    {
        this.workspaceService = workspaceService ??
            throw new ArgumentNullException(nameof(workspaceService));
        this.searchIndex = searchIndex ??
            throw new ArgumentNullException(nameof(searchIndex));
        this.secureFileProcessor = secureFileProcessor ??
            throw new ArgumentNullException(nameof(secureFileProcessor));
        this.keyRequestService = keyRequestService ??
            throw new ArgumentNullException(nameof(keyRequestService));

        WorkspaceContentSearchOptions options = searchIndex.Options;
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
        {
            throw new ArgumentException(
                LocalizationManager.GetString(
                    "Workspace.ContentSearch.EnterQuery"),
                nameof(query));
        }

        string root = workspaceService.WorkspaceDirectory;
        if(string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException(
                LocalizationManager.GetString("Workspace.NotSelected"));
        }
        root = Path.GetFullPath(root);
        if(!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                LocalizationManager.Format(
                    "Workspace.DirectoryNotFound",
                    root));
        }

        WorkspaceSearchIndexUpdateOutcome update = await searchIndex
            .UpdateAsync(root, progress, cancellationToken);
        IReadOnlyList<WorkspaceIndexedDocument> indexed = await searchIndex
            .SearchAsync(root, normalizedQuery, cancellationToken);

        WorkspaceContentSearchOptions options = searchIndex.Options;
        var results = new List<WorkspaceContentSearchResult>();
        foreach(WorkspaceIndexedDocument document in indexed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddIfMatched(
                results,
                document.Name,
                document.Path,
                document.RelativePath,
                document.Body,
                normalizedQuery,
                options.SnippetLength,
                isEncrypted: false);
        }

        int skippedEncryptedFileCount = 0;
        if(update.EncryptedFiles.Count > 0)
        {
            if(!keyRequestService.EnsureKeyAvailable())
            {
                skippedEncryptedFileCount = update.EncryptedFiles.Count;
            }
            else
            {
                int processedEncryptedCount = 0;
                foreach(string filePath in update.EncryptedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    processedEncryptedCount++;
                    string relativePath = Path.GetRelativePath(root, filePath);
                    progress?.Report(new WorkspaceContentSearchProgress(
                        processedEncryptedCount,
                        relativePath));

                    try
                    {
                        await using DecryptedFileContent decrypted =
                            await secureFileProcessor.DecryptFileContentAsync(
                                filePath,
                                cancellationToken: cancellationToken);
                        if(decrypted.Content.CanSeek &&
                           decrypted.Content.Length > options.MaxFileSizeBytes)
                        {
                            skippedEncryptedFileCount++;
                            continue;
                        }

                        IDocumentTextExtractor? extractor =
                            searchIndex.FindExtractor(
                                decrypted.OriginalExtension);
                        if(extractor is null)
                            continue;

                        string text = await extractor.ExtractAsync(
                            decrypted.Content,
                            decrypted.OriginalExtension,
                            cancellationToken);
                        AddIfMatched(
                            results,
                            Path.GetFileName(filePath),
                            filePath,
                            relativePath,
                            text,
                            normalizedQuery,
                            options.SnippetLength,
                            isEncrypted: true);
                    }
                    catch(OperationCanceledException)
                    {
                        throw;
                    }
                    catch(Exception exception) when(
                        exception is UnauthorizedAccessException or
                            IOException or
                            System.Security.Cryptography.CryptographicException or
                            NotSupportedException or
                            ArgumentException or
                            System.Xml.XmlException or
                            System.Windows.Markup.XamlParseException)
                    {
                        skippedEncryptedFileCount++;
                    }
                }
            }
        }

        WorkspaceContentSearchResult[] ordered = results
            .OrderBy(
                result => result.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                result => result.RelativePath,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        bool isTruncated = ordered.Length > options.MaxResults;
        if(isTruncated)
            ordered = ordered.Take(options.MaxResults).ToArray();

        return new WorkspaceContentSearchOutcome(
            ordered,
            isTruncated,
            update.SkippedDirectoryCount,
            update.SkippedFileCount,
            skippedEncryptedFileCount);
    }

    private static void AddIfMatched(
        ICollection<WorkspaceContentSearchResult> results,
        string name,
        string path,
        string relativePath,
        string text,
        string query,
        int snippetLength,
        bool isEncrypted)
    {
        int firstMatch = text.IndexOf(
            query,
            StringComparison.CurrentCultureIgnoreCase);
        if(firstMatch < 0)
            return;

        results.Add(new WorkspaceContentSearchResult(
            name,
            path,
            relativePath,
            CreateSnippet(
                text,
                firstMatch,
                query.Length,
                snippetLength),
            CountMatches(text, query),
            isEncrypted));
    }

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
        string normalized = NormalizeWhitespace(text.Substring(start, length));
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
