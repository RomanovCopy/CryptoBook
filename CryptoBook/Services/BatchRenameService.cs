using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System.IO;

namespace CryptoBook.Services;

/// <summary>Local files only. Every move forbids overwrite, including recovery moves.</summary>
public sealed class BatchRenameService: IBatchRenameService
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<IReadOnlyList<BatchRenameEntry>> PreviewAsync(
        IReadOnlyList<string> paths, BatchRenameOptions options,
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<BatchRenameEntry>>(() =>
    {
        var entries = new List<BatchRenameEntry>(paths.Count);
        DateTime currentTime = DateTime.Now;
        for(int i = 0; i < paths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = paths[i];
            try
            {
                if(!StorageLocation.Parse(path).IsLocal || !Path.IsPathFullyQualified(path))
                    throw new ArgumentException(BatchRenamePattern.Text("SourceMissing"));
                path = Path.GetFullPath(path);
                string name = BatchRenamePattern.BuildName(Path.GetFileName(path), i, options, currentTime);
                entries.Add(new(path, name, BatchRenamePattern.ValidateName(name)));
            }
            catch(Exception exception) when(exception is ArgumentException or OverflowException or
                System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                entries.Add(new(path, string.Empty, exception.Message));
            }
        }
        return Validate(entries, cancellationToken);
    }, cancellationToken);

    public async Task<BatchRenameResult> RenameAsync(
        IReadOnlyList<BatchRenameEntry> entries, IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Snapshot the reviewed plan before leaving the UI thread.
        BatchRenameEntry[] plan = entries.ToArray();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Execute(plan, progress, cancellationToken),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static IReadOnlyList<BatchRenameEntry> Validate(
        IReadOnlyList<BatchRenameEntry> entries, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var checkedEntries = entries.Select(entry =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? error = entry.Error ?? BatchRenamePattern.ValidateName(entry.NewName);
            if(error is null && (!Path.IsPathFullyQualified(entry.SourcePath) ||
                !StorageLocation.Parse(entry.SourcePath).IsLocal || !File.Exists(entry.SourcePath)))
                error = BatchRenamePattern.Text("SourceMissing");
            if(error is null && !seen.Add(Path.GetFullPath(entry.SourcePath)))
                error = BatchRenamePattern.Text("Duplicate");
            return entry with { Error = error };
        }).ToArray();
        var sources = checkedEntries.Where(e => e.Error is null && e.IsChanged)
            .Select(e => Path.GetFullPath(e.SourcePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicates = checkedEntries.Where(e => e.Error is null)
            .GroupBy(e => e.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return checkedEntries.Select(entry =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? error = entry.Error;
            if(error is null && duplicates.Contains(entry.TargetPath))
                error = BatchRenamePattern.Text("Duplicate");
            if(error is null && entry.IsChanged &&
               Path.Exists(entry.TargetPath) && !sources.Contains(entry.TargetPath))
                error = BatchRenamePattern.Text("TargetExists");
            return entry with { Error = error };
        }).ToArray();
    }

    private static BatchRenameResult Execute(
        IReadOnlyList<BatchRenameEntry> entries, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var states = new List<RenameState>();
        LocalOperationJournal? journal = null;
        try
        {
            var validated = Validate(entries, cancellationToken);
            if(validated.FirstOrDefault(e => e.Error is not null) is { } invalid)
                return new(false, false, invalid.OldName + ": " + invalid.Error,
                    new Dictionary<string, string>());
            var changed = validated.Where(e => e.IsChanged).ToArray();
            if(changed.Length == 0) return new(true, false, null, new Dictionary<string, string>());
            journal = LocalOperationJournal.Create(Path.GetDirectoryName(changed[0].SourcePath)!);
            foreach(var entry in changed)
            {
                states.Add(new RenameState(entry, journal.Allocate(entry.SourcePath)));
            }
            int completed = 0;
            foreach(var state in states)
            {
                cancellationToken.ThrowIfCancellationRequested();
                journal.Move(state.CurrentPath, state.TemporaryPath, state.Identity);
                state.CurrentPath = state.TemporaryPath;
                progress?.Report(++completed * 100 / (states.Count * 2));
            }
            foreach(var state in states)
            {
                cancellationToken.ThrowIfCancellationRequested();
                journal.Move(state.CurrentPath, state.Entry.TargetPath, state.Identity);
                state.CurrentPath = state.Entry.TargetPath;
                progress?.Report(++completed * 100 / (states.Count * 2));
            }
            cancellationToken.ThrowIfCancellationRequested();
            journal.Complete();
            return new(true, false, null, Changes(states));
        }
        catch(Exception exception)
        {
            var errors = new List<string>();
            if(journal is not null && !journal.IsCommitted && journal.Rollback() is { } recoveryError)
                errors.Add(recoveryError);
            foreach(var state in states)
                state.CurrentPath = new[] { state.Entry.SourcePath, state.TemporaryPath, state.Entry.TargetPath }
                    .FirstOrDefault(p => Path.Exists(p) && LocalOperationJournal.Identity(p) == state.Identity)
                    ?? state.CurrentPath;
            var changes = Changes(states);
            string? message = exception is OperationCanceledException ? null : exception.Message;
            if(changes.Count > 0)
                message = string.Join(Environment.NewLine,
                    new[] { message, BatchRenamePattern.Text("RecoveryRequired") }
                    .Concat(changes.Select(p => p.Key + " → " + p.Value))
                    .Where(s => !string.IsNullOrEmpty(s)));
            return new(false, exception is OperationCanceledException,
                errors.Count > 0 ? string.Join(Environment.NewLine, new[] { message }.Concat(errors)) : message, changes);
        }
        finally { journal?.Dispose(); }
    }

    private static IReadOnlyDictionary<string, string> Changes(IEnumerable<RenameState> states) =>
        states.Where(s => !string.Equals(s.Entry.SourcePath, s.CurrentPath, StringComparison.Ordinal))
            .ToDictionary(s => s.Entry.SourcePath, s => s.CurrentPath, StringComparer.OrdinalIgnoreCase);

    private sealed class RenameState(BatchRenameEntry entry, string temporaryPath)
    {
        public string Identity { get; } = LocalOperationJournal.Identity(entry.SourcePath);
        public BatchRenameEntry Entry { get; } = entry;
        public string TemporaryPath { get; } = temporaryPath;
        public string CurrentPath { get; set; } = entry.SourcePath;
    }
}
