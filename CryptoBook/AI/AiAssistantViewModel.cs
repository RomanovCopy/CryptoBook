using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace CryptoBook.AI;

public sealed class AiChatMessage(string role, string text, IReadOnlyList<AiAttachment>? attachments = null) : ViewModelBase
{
    private string content = text;
    public string Role { get; } = role;
    public string RoleLabel => LocalizationManager.GetString(Role == "user" ? "AI.You" : "AI.Assistant");
    public string Content { get => content; set => SetProperty(ref content, value); }
    public IReadOnlyList<AiAttachment> Attachments { get; } = attachments ?? [];
}

public sealed class AiAssistantViewModel : ViewModelBase, IDisposable
{
    private readonly AiSettingsStore store;
    private readonly IAiInferenceService inference;
    private readonly AiEditorContext editor;
    private readonly AiSettingsWindowService settingsWindow;
    private readonly IKeyResetService keyReset;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer idleTimer;
    private readonly AiAttachmentService attachmentService;
    private CancellationTokenSource? cancellation;
    private AiEditTarget? responseTarget;
    private AiChatMessage? lastResponse;
    private bool isOpen, isBusy, isAttaching, disposed;
    private long operationVersion;
    private string prompt = string.Empty, status = string.Empty;

    public AiAssistantViewModel(AiSettingsStore store, IAiInferenceService inference,
        AiEditorContext editor, AiSettingsWindowService settingsWindow, IKeyResetService keyReset,
        Dispatcher dispatcher, AiAttachmentService? attachmentService = null)
    {
        this.store = store; this.inference = inference; this.editor = editor;
        this.settingsWindow = settingsWindow; this.keyReset = keyReset; this.dispatcher = dispatcher;
        this.attachmentService = attachmentService ?? new AiAttachmentService([]);
        Send = new AsyncRelayCommand((_, token) => SendAsync(token), _ =>
            Enabled && !IsBusy && !isAttaching && (!string.IsNullOrWhiteSpace(Prompt) || HasAttachments) && !IsLocked);
        Stop = new RelayCommand(_ => cancellation?.Cancel(), _ => IsBusy);
        Toggle = new RelayCommand(_ => IsOpen = !IsOpen);
        OpenPanel = new RelayCommand(_ => IsOpen = true);
        OpenSettings = new RelayCommand(_ => settingsWindow.Open());
        AttachFiles = new AsyncRelayCommand(async (_, token) =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationManager.GetString("AI.AttachFiles"),
                Filter = LocalizationManager.GetString("AI.AttachmentFilter"), Multiselect = true, CheckFileExists = true
            };
            if(picker.ShowDialog() == true) await AttachPathsAsync(picker.FileNames, token);
        }, _ => CanAttach);
        AttachDocument = new RelayCommand(_ => AttachCurrentDocument(), _ => CanAttach);
        RemoveAttachment = new RelayCommand(file => { if(file is AiAttachment attachment) Attachments.Remove(attachment); }, _ => !IsBusy && !isAttaching);
        ClearChat = new RelayCommand(_ => Clear(), _ => !IsBusy);
        Insert = new RelayCommand(_ => Apply(false), _ => CanApply && responseTarget is not null);
        Replace = new RelayCommand(_ => Apply(true), _ => CanApply && responseTarget?.HasSelection == true);
        Copy = new RelayCommand(_ => CopyResponse(), _ => CanApply);
        Unload = new AsyncRelayCommand(async (_, _) =>
        {
            operationVersion++;
            cancellation?.Cancel();
            await inference.UnloadAsync();
            Status = LocalizationManager.GetString("AI.Unloaded");
            Refresh();
        });
        idleTimer = new DispatcherTimer(TimeSpan.FromSeconds(15), DispatcherPriority.Background, OnIdle, dispatcher);
        store.Changed += OnSettingsChanged;
        keyReset.StateChanged += OnKeyStateChanged;
        LocalizationManager.CultureChanged += OnCultureChanged;
        Attachments.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasAttachments), nameof(CanAttach)); Refresh(); };
    }

    public ObservableCollection<AiChatMessage> Messages { get; } = [];
    public ObservableCollection<AiAttachment> Attachments { get; } = [];
    public void BindWorkspace(IPageNavigationService navigation) => editor.BindWorkspace(navigation);
    public bool IsOpen { get => isOpen; set => SetProperty(ref isOpen, value); }
    public bool IsBusy { get => isBusy; private set { SetProperty(ref isBusy, value); Refresh(); } }
    public bool IsModelLoaded => inference.IsLoaded;
    public bool Enabled
    {
        get => store.Current.Enabled;
        set
        {
            try { store.Save(store.Current with { Enabled = value }); }
            catch(Exception) { Status = LocalizationManager.GetString("AI.SaveFailed"); }
        }
    }
    public string ModelName => string.IsNullOrWhiteSpace(store.Current.SelectedModelPath)
        ? LocalizationManager.GetString("AI.NoModel") : System.IO.Path.GetFileNameWithoutExtension(store.Current.SelectedModelPath);
    public string Prompt { get => prompt; set { SetProperty(ref prompt, value); Send.RaiseCanExecuteChanged(); } }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool HasAttachments => Attachments.Count > 0;
    public bool CanAttach => !IsBusy && !isAttaching && !IsLocked && Attachments.Count < AiAttachmentService.MaxFiles;
    private bool IsLocked => keyReset.State is KeyResetState.Resetting or KeyResetState.KeyReset or KeyResetState.Unlocking or KeyResetState.Restoring;
    private bool CanApply => !IsBusy && !IsLocked && !string.IsNullOrWhiteSpace(lastResponse?.Content);
    public AsyncRelayCommand Send { get; }
    public ICommand Stop { get; }
    public ICommand Toggle { get; }
    public ICommand OpenPanel { get; }
    public ICommand OpenSettings { get; }
    public AsyncRelayCommand AttachFiles { get; }
    public ICommand AttachDocument { get; }
    public ICommand RemoveAttachment { get; }
    public ICommand ClearChat { get; }
    public ICommand Insert { get; }
    public ICommand Replace { get; }
    public ICommand Copy { get; }
    public AsyncRelayCommand Unload { get; }

    private void AttachCurrentDocument()
    {
        var target = editor.Capture();
        try
        {
            if(target is null || string.IsNullOrWhiteSpace(target.DocumentText)) throw new AiException("AI.NoDocument");
            if(!CanAttach) return;
            string name = System.IO.Path.HasExtension(target.Name) ? target.Name : target.Name + ".txt";
            Attachments.Add(AiAttachmentService.Document(name, target.DocumentText));
            Status = string.Empty;
        }
        catch(Exception error) { Status = ErrorText(error); }
    }

    public async Task AttachPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        if(!CanAttach) return;
        long version = operationVersion;
        isAttaching = true; Refresh();
        try
        {
            foreach(string path in paths)
            {
                if(Attachments.Count >= AiAttachmentService.MaxFiles) throw new AiException("AI.AttachmentLimit");
                var file = await attachmentService.ReadAsync(path, cancellationToken);
                if(disposed || IsLocked || version != operationVersion) return;
                Attachments.Add(file);
            }
            Status = string.Empty;
        }
        catch(OperationCanceledException) { }
        catch(Exception error) { if(version == operationVersion) Status = error is AiException ? ErrorText(error) : LocalizationManager.GetString("AI.AttachmentReadFailed"); }
        finally { isAttaching = false; Refresh(); }
    }

    private async Task SendAsync(CancellationToken commandToken)
    {
        if(IsBusy || isAttaching || IsLocked || !Enabled) return;
        string input = Prompt.Trim();
        if(input.Length == 0 && HasAttachments) input = LocalizationManager.GetString("AI.DefaultFilePrompt");
        var files = Attachments.ToArray();
        try
        {
            store.Current.SelectedModel.Validate();
            if(files.Any(file => file.IsImage) || Messages.Any(message => message.Attachments.Any(file => file.IsImage)))
                _ = AiVisionProjector.Find(store.Current.SelectedModelPath);
        }
        catch(Exception e) { Status = ErrorText(e); return; }
        responseTarget = editor.Capture();
        Attachments.Clear();
        Prompt = string.Empty;
        var conversation = Messages.Select(m => new AiTurn(m.Role, m.Content, m.Attachments)).ToList();
        conversation.Add(new("user", input, files));
        Messages.Add(new("user", input, files));
        var answer = new AiChatMessage("assistant", string.Empty);
        Messages.Add(answer);
        lastResponse = answer;
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken);
        long version = ++operationVersion;
        cancellation = requestCancellation;
        IsBusy = true;
        Status = LocalizationManager.GetString("AI.Loading");
        try
        {
            bool hasImages = conversation.Any(turn => turn.Attachments?.Any(file => file.IsImage) == true);
            var progress = new Progress<float>(value =>
            {
                if(!requestCancellation.IsCancellationRequested && !disposed && version == operationVersion)
                    Status = value >= 1 ? LocalizationManager.GetString(hasImages && answer.Content.Length == 0 ? "AI.ProcessingImage" : "AI.Generating")
                        : LocalizationManager.Format("AI.LoadProgress", (int)(value * 100));
            });
            await inference.GenerateAsync(store.Current.SelectedModel, conversation, text =>
            {
                dispatcher.InvokeAsync(() =>
                {
                    if(!requestCancellation.IsCancellationRequested && !disposed && version == operationVersion)
                    {
                        answer.Content += text;
                        Status = LocalizationManager.GetString("AI.Generating");
                    }
                }, DispatcherPriority.Normal);
            }, progress, requestCancellation.Token);
            // Drain queued stream fragments before evaluating the final answer.
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            if(version == operationVersion)
                Status = LocalizationManager.GetString(string.IsNullOrWhiteSpace(answer.Content) ? "AI.EmptyAnswer" : "AI.Ready");
        }
        catch(OperationCanceledException) { if(version == operationVersion) Status = LocalizationManager.GetString("AI.Stopped"); }
        catch(Exception e) { if(version == operationVersion) Status = ErrorText(e); }
        finally
        {
            cancellation = null;
            if(string.IsNullOrWhiteSpace(answer.Content)) Messages.Remove(answer);
            IsBusy = false;
        }
    }

    private void Apply(bool replace)
    {
        if(!CanApply) return;
        if(responseTarget?.Apply(lastResponse!.Content, replace) != true)
            Status = LocalizationManager.GetString("AI.DocumentChanged");
        else { responseTarget = null; Status = LocalizationManager.GetString("AI.Inserted"); Refresh(); }
    }

    private void CopyResponse()
    {
        try { System.Windows.Clipboard.SetText(lastResponse!.Content); }
        catch(System.Runtime.InteropServices.ExternalException) { Status = LocalizationManager.GetString("AI.CopyFailed"); }
    }

    private void Clear()
    {
        operationVersion++;
        Messages.Clear(); lastResponse = null; responseTarget = null;
        AttachFiles.Cancel();
        Prompt = string.Empty; Attachments.Clear(); Status = string.Empty; Refresh();
    }
    private void Refresh()
    {
        OnPropertyChanged(nameof(IsModelLoaded));
        OnPropertyChanged(nameof(CanAttach));
        Send?.RaiseCanExecuteChanged();
        AttachFiles?.RaiseCanExecuteChanged();
        foreach(ICommand command in new[] { Stop, AttachDocument, RemoveAttachment, ClearChat, Insert, Replace, Copy })
            (command as RelayCommand)?.RaiseCanExecuteChanged();
    }
    private async void OnIdle(object? sender, EventArgs e)
    {
        await CheckIdleAsync(DateTime.UtcNow);
    }
    internal async Task CheckIdleAsync(DateTime utcNow)
    {
        if(!IsBusy && !inference.IsBusy && store.Current.IdleMinutes > 0 && inference.IsLoaded &&
           utcNow - inference.LastUsedUtc >= TimeSpan.FromMinutes(store.Current.IdleMinutes))
        {
            await inference.UnloadAsync();
            Status = LocalizationManager.GetString("AI.Unloaded"); Refresh();
        }
    }
    private async void OnSettingsChanged(object? sender, EventArgs e)
    {
        cancellation?.Cancel();
        Clear();
        OnPropertyChanged(nameof(Enabled), nameof(ModelName));
        try { await inference.UnloadAsync(); }
        catch(Exception error) { Status = ErrorText(error); }
        finally { Refresh(); }
    }
    private void OnKeyStateChanged(object? sender, KeyResetStateChangedEventArgs e)
    {
        if(e.State != KeyResetState.Resetting)
        {
            dispatcher.InvokeAsync(Refresh);
            return;
        }
        dispatcher.InvokeAsync(async () =>
        {
            cancellation?.Cancel(); Clear();
            await inference.UnloadAsync(); Refresh();
        });
    }
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ModelName));
        foreach(var message in Messages) message.OnPropertyChanged(nameof(AiChatMessage.RoleLabel));
    }
    public static string ErrorText(Exception error)
    {
        string message = LocalizationManager.GetString(error is AiException ai ? ai.ResourceKey : "AI.GenerationFailed");
        if(error is not AiException || error.InnerException is not null)
            message += " " + error.GetBaseException().Message;
        return message;
    }
    public void Dispose()
    {
        disposed = true; cancellation?.Cancel(); idleTimer.Stop();
        store.Changed -= OnSettingsChanged;
        keyReset.StateChanged -= OnKeyStateChanged;
        LocalizationManager.CultureChanged -= OnCultureChanged;
        Clear();
    }
}
