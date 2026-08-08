using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using Microsoft.Data.Sqlite;

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoBook.Services;

/// <summary>
/// Постоянный FTS5-каталог. При обновлении повторно извлекает только изменившиеся файлы.
/// Расшифрованное содержимое защищённых документов в каталог не записывается.
/// </summary>
public sealed class WorkspaceSearchIndex: IWorkspaceSearchIndex
{
    private const int SchemaVersion = 1;
    private const int IndexedState = 0;
    private const int SkippedState = 1;
    private const int EncryptedState = 2;
    private const long EncryptedContainerOverheadAllowance = 2 * 1024 * 1024;

    private readonly ISecureFileValidator secureFileValidator;
    private readonly IReadOnlyList<IDocumentTextExtractor> extractors;
    private readonly SemaphoreSlim gate = new(1, 1);

    public WorkspaceSearchIndex(
        ISecureFileValidator secureFileValidator,
        IEnumerable<IDocumentTextExtractor> extractors,
        WorkspaceContentSearchOptions options)
    {
        this.secureFileValidator = secureFileValidator ??
            throw new ArgumentNullException(nameof(secureFileValidator));
        this.extractors = extractors?.ToArray() ??
            throw new ArgumentNullException(nameof(extractors));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public WorkspaceContentSearchOptions Options { get; }

    public IDocumentTextExtractor? FindExtractor(string extension) =>
        extractors.FirstOrDefault(extractor => extractor.CanExtract(extension));

    public async Task<WorkspaceSearchIndexUpdateOutcome> UpdateAsync(
        string workspaceRoot,
        IProgress<WorkspaceContentSearchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string root = NormalizeRoot(workspaceRoot);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using SqliteConnection connection = await OpenAsync(
                root,
                cancellationToken);
            Dictionary<string, CatalogEntry> existing = await LoadCatalogAsync(
                connection,
                cancellationToken);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var remove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int processedFileCount = 0;
            int skippedDirectoryCount = 0;

            await using SqliteTransaction transaction =
                connection.BeginTransaction();
            var pendingDirectories = new Stack<string>();
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

                foreach(string rawPath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string path = Path.GetFullPath(rawPath);
                    string extension = Path.GetExtension(path);
                    IDocumentTextExtractor? extractor = FindExtractor(extension);
                    bool secureCandidate = extension.Equals(
                            ".cbook",
                            StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(
                            ".cbox",
                            StringComparison.OrdinalIgnoreCase);
                    if(extractor is null && !secureCandidate)
                        continue;

                    processedFileCount++;
                    string relativePath = Path.GetRelativePath(root, path);
                    progress?.Report(new WorkspaceContentSearchProgress(
                        processedFileCount,
                        relativePath));
                    seen.Add(path);

                    try
                    {
                        var info = new FileInfo(path);
                        long modifiedTicks = info.LastWriteTimeUtc.Ticks;
                        long length = info.Length;
                        if(existing.TryGetValue(path, out CatalogEntry? current) &&
                           current.ModifiedTicks == modifiedTicks &&
                           current.Length == length)
                        {
                            continue;
                        }

                        bool encrypted = await secureFileValidator
                            .HasCryptoBookHeaderAsync(path, cancellationToken);
                        if(encrypted)
                        {
                            int state = length >
                                Options.MaxFileSizeBytes +
                                EncryptedContainerOverheadAllowance
                                ? SkippedState
                                : EncryptedState;
                            await UpsertAsync(
                                connection,
                                transaction,
                                path,
                                relativePath,
                                extension,
                                modifiedTicks,
                                length,
                                state,
                                string.Empty,
                                cancellationToken);
                            continue;
                        }

                        if(extractor is null)
                        {
                            remove.Add(path);
                            continue;
                        }

                        if(length > Options.MaxFileSizeBytes)
                        {
                            await UpsertAsync(
                                connection,
                                transaction,
                                path,
                                relativePath,
                                extension,
                                modifiedTicks,
                                length,
                                SkippedState,
                                string.Empty,
                                cancellationToken);
                            continue;
                        }

                        await using FileStream stream = new(
                            path,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete,
                            81920,
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan);
                        string body = await extractor.ExtractAsync(
                            stream,
                            extension,
                            cancellationToken);
                        await UpsertAsync(
                            connection,
                            transaction,
                            path,
                            relativePath,
                            extension,
                            modifiedTicks,
                            length,
                            IndexedState,
                            body,
                            cancellationToken);
                    }
                    catch(OperationCanceledException)
                    {
                        throw;
                    }
                    catch(Exception exception) when(
                        exception is UnauthorizedAccessException or
                            IOException or
                            CryptographicException or
                            NotSupportedException or
                            ArgumentException or
                            System.Xml.XmlException or
                            System.Windows.Markup.XamlParseException)
                    {
                        FileInfo info = new(path);
                        await UpsertAsync(
                            connection,
                            transaction,
                            path,
                            relativePath,
                            extension,
                            info.Exists ? info.LastWriteTimeUtc.Ticks : 0,
                            info.Exists ? info.Length : 0,
                            SkippedState,
                            string.Empty,
                            cancellationToken);
                    }
                }

                foreach(string childDirectory in childDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if((File.GetAttributes(childDirectory) &
                            FileAttributes.ReparsePoint) == 0)
                        {
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
            }

            foreach(string path in existing.Keys)
            {
                if(!seen.Contains(path))
                    remove.Add(path);
            }
            foreach(string path in remove)
            {
                await DeleteAsync(
                    connection,
                    transaction,
                    path,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            IReadOnlyList<string> encryptedFiles = await LoadPathsByStateAsync(
                connection,
                EncryptedState,
                cancellationToken);
            int skippedFileCount = await CountByStateAsync(
                connection,
                SkippedState,
                cancellationToken);
            return new WorkspaceSearchIndexUpdateOutcome(
                encryptedFiles,
                skippedDirectoryCount,
                skippedFileCount);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<WorkspaceIndexedDocument>> SearchAsync(
        string workspaceRoot,
        string query,
        CancellationToken cancellationToken = default)
    {
        string root = NormalizeRoot(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using SqliteConnection connection = await OpenAsync(
                root,
                cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            if(query.Length >= 3)
            {
                command.CommandText =
                    """
                    SELECT d.name, d.path, d.relative_path, d.body
                    FROM document_fts AS f
                    JOIN documents AS d ON d.id = f.rowid
                    WHERE document_fts MATCH $query AND d.state = 0;
                    """;
                command.Parameters.AddWithValue(
                    "$query",
                    $"\"{query.Replace("\"", "\"\"")}\"");
            }
            else
            {
                // FTS5-trigram не индексирует запросы короче трёх символов.
                command.CommandText =
                    """
                    SELECT name, path, relative_path, body
                    FROM documents
                    WHERE state = 0;
                    """;
            }

            var result = new List<WorkspaceIndexedDocument>();
            await using SqliteDataReader reader =
                await command.ExecuteReaderAsync(cancellationToken);
            while(await reader.ReadAsync(cancellationToken))
            {
                result.Add(new WorkspaceIndexedDocument(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SqliteConnection> OpenAsync(
        string root,
        CancellationToken cancellationToken)
    {
        string path = GetDatabasePath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = false
            }.ToString());
        await connection.OpenAsync(cancellationToken);
        await InitializeSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private static async Task InitializeSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using(SqliteCommand versionCommand = connection.CreateCommand())
        {
            versionCommand.CommandText = "PRAGMA user_version;";
            int version = Convert.ToInt32(
                await versionCommand.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            if(version is not 0 && version != SchemaVersion)
            {
                versionCommand.CommandText =
                    """
                    DROP TRIGGER IF EXISTS documents_ai;
                    DROP TRIGGER IF EXISTS documents_ad;
                    DROP TRIGGER IF EXISTS documents_au;
                    DROP TABLE IF EXISTS document_fts;
                    DROP TABLE IF EXISTS documents;
                    PRAGMA user_version=0;
                    """;
                await versionCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $$"""
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            CREATE TABLE IF NOT EXISTS documents(
                id INTEGER PRIMARY KEY,
                path TEXT NOT NULL UNIQUE COLLATE NOCASE,
                name TEXT NOT NULL,
                relative_path TEXT NOT NULL,
                extension TEXT NOT NULL,
                modified_ticks INTEGER NOT NULL,
                length INTEGER NOT NULL,
                state INTEGER NOT NULL,
                body TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_documents_state ON documents(state);
            CREATE VIRTUAL TABLE IF NOT EXISTS document_fts USING fts5(
                body,
                content='documents',
                content_rowid='id',
                tokenize='trigram case_sensitive 0'
            );
            CREATE TRIGGER IF NOT EXISTS documents_ai AFTER INSERT ON documents BEGIN
                INSERT INTO document_fts(rowid, body) VALUES (new.id, new.body);
            END;
            CREATE TRIGGER IF NOT EXISTS documents_ad AFTER DELETE ON documents BEGIN
                INSERT INTO document_fts(document_fts, rowid, body)
                VALUES ('delete', old.id, old.body);
            END;
            CREATE TRIGGER IF NOT EXISTS documents_au AFTER UPDATE ON documents BEGIN
                INSERT INTO document_fts(document_fts, rowid, body)
                VALUES ('delete', old.id, old.body);
                INSERT INTO document_fts(rowid, body) VALUES (new.id, new.body);
            END;
            PRAGMA user_version={{SchemaVersion}};
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, CatalogEntry>> LoadCatalogAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT path, modified_ticks, length FROM documents;";
        var result = new Dictionary<string, CatalogEntry>(
            StringComparer.OrdinalIgnoreCase);
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = new CatalogEntry(
                reader.GetInt64(1),
                reader.GetInt64(2));
        }
        return result;
    }

    private static async Task UpsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string path,
        string relativePath,
        string extension,
        long modifiedTicks,
        long length,
        int state,
        string body,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO documents(
                path, name, relative_path, extension,
                modified_ticks, length, state, body)
            VALUES(
                $path, $name, $relativePath, $extension,
                $modifiedTicks, $length, $state, $body)
            ON CONFLICT(path) DO UPDATE SET
                name = excluded.name,
                relative_path = excluded.relative_path,
                extension = excluded.extension,
                modified_ticks = excluded.modified_ticks,
                length = excluded.length,
                state = excluded.state,
                body = excluded.body;
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$name", Path.GetFileName(path));
        command.Parameters.AddWithValue("$relativePath", relativePath);
        command.Parameters.AddWithValue("$extension", extension);
        command.Parameters.AddWithValue("$modifiedTicks", modifiedTicks);
        command.Parameters.AddWithValue("$length", length);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$body", body);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string path,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM documents WHERE path = $path;";
        command.Parameters.AddWithValue("$path", path);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> LoadPathsByStateAsync(
        SqliteConnection connection,
        int state,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT path FROM documents WHERE state = $state;";
        command.Parameters.AddWithValue("$state", state);
        var result = new List<string>();
        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken))
            result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<int> CountByStateAsync(
        SqliteConnection connection,
        int state,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM documents WHERE state = $state;";
        command.Parameters.AddWithValue("$state", state);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private string GetDatabasePath(string root)
    {
        string directory = Options.IndexDirectory ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "CryptoBook",
            "Search");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            root.ToUpperInvariant()));
        string name = Convert.ToHexString(hash.AsSpan(0, 16)) + ".db";
        return Path.Combine(directory, name);
    }

    private static string NormalizeRoot(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        string root = Path.GetFullPath(workspaceRoot);
        if(!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);
        return root;
    }

    private sealed record CatalogEntry(long ModifiedTicks, long Length);
}
