using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace CryptoBook.ViewModels;

public sealed class BatchRenameViewModel: ViewModelBase, IViewModel, IWindowWithId, IDisposable
{
    public const string FilesContextKey = "batchRenameFiles";
    private readonly IBatchRenameService service;
    private readonly IWindowManager windows;
    private readonly IDocumentSession session;
    private readonly IPinnedDocumentService pinned;
    private readonly IRecentDocumentService recent;
    private readonly string[] paths;
    private readonly DispatcherTimer previewTimer;
    private CancellationTokenSource? previewCancellation;
    private CancellationTokenSource? operationCancellation;
    private BatchRenameOptions options = new();
    private IReadOnlyList<BatchRenameEntry> entries = [];
    private IReadOnlyList<BatchRenameEntry>? undoPlan;
    private bool disposed;
    private bool isBusy;
    private bool previewPending = true;
    private bool completed;
    private string status = string.Empty;
    private string linksWarning = string.Empty;
    private int progress;
    private int previewVersion;

    public BatchRenameViewModel(IWindowContext context, IBatchRenameService service,
        IWindowManager windows, IDocumentSession session,
        IPinnedDocumentService pinned, IRecentDocumentService recent)
    {
        this.service = service;
        this.windows = windows;
        this.session = session;
        this.pinned = pinned;
        this.recent = recent;
        paths = context.Get<IReadOnlyList<string>>(FilesContextKey).ToArray();
        previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        previewTimer.Tick += PreviewTimerTick;
        ApplyCommand = new AsyncRelayCommand((_, _) => RunAsync(false), _ => CanApply);
        UndoCommand = new AsyncRelayCommand((_, _) => RunAsync(true), _ => !IsBusy && undoPlan is not null);
        RefreshPreviewCommand = new AsyncRelayCommand((_, _) => RefreshPreviewAsync(), _ => !IsBusy && !completed);
        CancelCommand = new RelayCommand(_ => operationCancellation?.Cancel(), _ => IsBusy);
        Close = new RelayCommand(_ =>
        {
            if(IsBusy) operationCancellation?.Cancel();
            else windows.CloseWindow(WindowId);
        });
        Closing = new RelayCommand(parameter =>
        {
            if(IsBusy && parameter is CancelEventArgs e)
            {
                e.Cancel = true;
                operationCancellation?.Cancel();
            }
        });
        Closed = new RelayCommand(_ => Dispose());
        Loaded = new AsyncRelayCommand((_, _) => RefreshPreviewAsync());
    }

    public Guid WindowId { get; } = Guid.NewGuid();
    public string NameMask { get => options.NameMask; set => Update(options with { NameMask = value }); }
    public string ExtensionMask { get => options.ExtensionMask; set => Update(options with { ExtensionMask = value }); }
    public string Search { get => options.Search; set => Update(options with { Search = value }); }
    public string Replacement { get => options.Replacement; set => Update(options with { Replacement = value }); }
    public bool UseRegex { get => options.UseRegex; set => Update(options with { UseRegex = value }); }
    public bool MatchCase { get => options.MatchCase; set => Update(options with { MatchCase = value }); }
    public string CounterStart { get => counterStart; set { if(SetProperty(ref counterStart, value)) SchedulePreview(); } }
    private string counterStart = "1";
    public string CounterStep { get => counterStep; set { if(SetProperty(ref counterStep, value)) SchedulePreview(); } }
    private string counterStep = "1";
    public string CounterDigits { get => counterDigits; set { if(SetProperty(ref counterDigits, value)) SchedulePreview(); } }
    private string counterDigits = "3";
    public int CaseIndex { get => (int)options.Case; set => Update(options with { Case = (RenameCase)value }); }
    public IReadOnlyList<BatchRenameEntry> Entries { get => entries; private set => SetProperty(ref entries, value); }
    public bool IsBusy { get => isBusy; private set { SetProperty(ref isBusy, value); NotifyCommands(); } }
    public bool CanEdit => !IsBusy && !completed;
    public bool CanApply => CanEdit && !previewPending && Entries.Count > 0 &&
        Entries.All(e => e.Error is null) && Entries.Any(e => e.IsChanged);
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string LinksWarning { get => linksWarning; private set => SetProperty(ref linksWarning, value); }
    public int Progress { get => progress; private set => SetProperty(ref progress, value); }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand UndoCommand { get; }
    public AsyncRelayCommand RefreshPreviewCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand Loaded { get; }
    public ICommand Close { get; }
    public ICommand Closing { get; }
    public ICommand Closed { get; }

    private void Update(BatchRenameOptions value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if(options == value) return;
        options = value;
        OnPropertyChanged(property);
        SchedulePreview();
    }

    private void SchedulePreview()
    {
        if(disposed || IsBusy || completed) return;
        previewVersion++;
        previewCancellation?.Cancel();
        previewPending = true;
        Status = BatchRenamePattern.Text("Previewing");
        NotifyCommands();
        previewTimer.Stop();
        previewTimer.Start();
    }

    private async void PreviewTimerTick(object? sender, EventArgs e) => await RefreshPreviewAsync();

    public async Task RefreshPreviewAsync()
    {
        if(disposed || IsBusy || completed) return;
        previewTimer.Stop();
        int version = ++previewVersion;
        previewCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        previewCancellation = cancellation;
        previewPending = true;
        Status = BatchRenamePattern.Text("Previewing");
        NotifyCommands();
        try
        {
            if(!long.TryParse(CounterStart, out long start) || start < 0 ||
               !long.TryParse(CounterStep, out long step) || step < 1 ||
               !int.TryParse(CounterDigits, out int digits) || digits is < 1 or > 18)
                throw new ArgumentException(BatchRenamePattern.Text("CounterInvalid"));
            var result = await service.PreviewAsync(paths, options with
            {
                CounterStart = start, CounterStep = step, CounterDigits = digits
            }, cancellation.Token);
            if(disposed || version != previewVersion) return;
            Entries = result;
            previewPending = false;
            Status = LocalizationManager.Format("BatchRename.PreviewStatus",
                Entries.Count(e => e.IsChanged), Entries.Count(e => e.Error is not null));
        }
        catch(OperationCanceledException) { }
        catch(Exception exception)
        {
            if(!disposed && version == previewVersion)
            {
                Entries = [];
                Status = exception.Message;
            }
        }
        finally
        {
            if(ReferenceEquals(previewCancellation, cancellation)) previewCancellation = null;
            if(!disposed) NotifyCommands();
        }
    }

    private async Task RunAsync(bool undo)
    {
        var plan = undo ? undoPlan! : Entries;
        IsBusy = true;
        Progress = 0;
        Status = BatchRenamePattern.Text("Running");
        using var cancellation = new CancellationTokenSource();
        operationCancellation = cancellation;
        try
        {
            BatchRenameResult result = await service.RenameAsync(plan,
                new Progress<int>(value => Progress = value), cancellation.Token);
            // Synchronize actual locations even if only part of recovery was possible.
            if(result.ChangedPaths.Count > 0)
                await SynchronizeLinksAsync(result.ChangedPaths);
            if(result.Success)
            {
                completed = !undo;
                undoPlan = undo ? null : result.ChangedPaths.Select(p =>
                    new BatchRenameEntry(p.Value, Path.GetFileName(p.Key))).ToArray();
                Status = BatchRenamePattern.Text(undo ? "Undone" : "Done");
            }
            else
            {
                Status = result.Error ?? BatchRenamePattern.Text(result.Cancelled ? "Cancelled" : "Failed");
                if(result.ChangedPaths.Count > 0)
                {
                    completed = true;
                    undoPlan = null;
                }
            }
        }
        catch(OperationCanceledException) { Status = BatchRenamePattern.Text("Cancelled"); }
        catch(Exception exception) { Status = exception.Message; }
        finally
        {
            operationCancellation = null;
            IsBusy = false;
        }
    }

    private async Task SynchronizeLinksAsync(IReadOnlyDictionary<string, string> mapping)
    {
        if(session.FilePath is { } current && mapping.TryGetValue(current, out string? target))
            session.Rename(target);
        var errors = new List<string>();
        try { await pinned.UpdatePathsAsync(mapping); }
        catch(Exception e) { errors.Add(e.Message); }
        try { await recent.UpdatePathsAsync(mapping); }
        catch(Exception e) { errors.Add(e.Message); }
        if(errors.Count > 0)
            LinksWarning = LocalizationManager.GetString("DocumentLinks.RenameSyncFailed") +
                Environment.NewLine + string.Join(Environment.NewLine, errors);
    }

    private void NotifyCommands()
    {
        OnPropertyChanged(nameof(CanEdit), nameof(CanApply));
        ApplyCommand.RaiseCanExecuteChanged();
        UndoCommand.RaiseCanExecuteChanged();
        RefreshPreviewCommand.RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if(disposed) return;
        disposed = true;
        previewTimer.Stop();
        previewTimer.Tick -= PreviewTimerTick;
        previewCancellation?.Cancel();
        operationCancellation?.Cancel();
    }
}
