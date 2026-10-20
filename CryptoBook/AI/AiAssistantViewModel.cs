using CryptoBook.Behaviors;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace CryptoBook.AI;

public sealed class AiChatMessage(string role, string text, IReadOnlyList<AiAttachment>? attachments = null) : ViewModelBase
{
    private string content = text;
    private string reasoning = string.Empty;
    public string Role { get; } = role;
    public string RoleLabel => LocalizationManager.GetString(Role == "user" ? "AI.You" : "AI.Assistant");
    public string Content { get => content; set => SetProperty(ref content, value); }
    public string Reasoning
    {
        get => reasoning;
        set { if(SetProperty(ref reasoning, value)) OnPropertyChanged(nameof(HasReasoning)); }
    }
    public bool HasReasoning => !string.IsNullOrWhiteSpace(reasoning);
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
    private readonly DispatcherTimer contextTimer;
    private readonly HashSet<AiChatMessage> observedMessages = [];
    private AiConfiguration previousConfiguration;
    private AiTokenUsage? tokenUsage;
    private int requestEstimate, contextCorrection;
    private bool contextDirty = true, contextIsEstimate = true;
    private int contextLength, remainingTokens, replyTokenLimit;
    private readonly AiAttachmentService attachmentService;
    private CancellationTokenSource? cancellation;
    private AiChatMessage? lastResponse;
    private bool isOpen, isBusy, isAttaching, disposed;
    private long operationVersion;
    private string prompt = string.Empty, status = string.Empty;

    public AiAssistantViewModel(AiSettingsStore store, IAiInferenceService inference,
        AiEditorContext editor, AiSettingsWindowService settingsWindow, IKeyResetService keyReset,
        Dispatcher dispatcher, AiAttachmentService? attachmentService = null, IUriNavigationService? uriNavigation = null)
    {
        this.store = store; this.inference = inference; this.editor = editor;
        previousConfiguration = store.Current;
        this.settingsWindow = settingsWindow; this.keyReset = keyReset; this.dispatcher = dispatcher;
        this.attachmentService = attachmentService ?? new AiAttachmentService([]);
        Runtime = new(inference);
        Send = new AsyncRelayCommand((_, token) => SendAsync(token), _ =>
            Enabled && !IsBusy && !isAttaching && (!string.IsNullOrWhiteSpace(Prompt) || HasAttachments) && !IsLocked);
        Stop = new RelayCommand(_ => cancellation?.Cancel(), _ => IsBusy);
        Toggle = new RelayCommand(_ => IsOpen = !IsOpen);
        OpenPanel = new RelayCommand(_ => IsOpen = true);
        OpenSettings = new RelayCommand(_ => settingsWindow.Open());
        OpenAppearance = new RelayCommand(_ => settingsWindow.OpenAppearance());
        var links = uriNavigation ?? new Services.UriNavigationService();
        OpenHyperlink = new RelayCommand(value => links.TryOpen((Uri)value!), value => value is Uri uri && uri.IsAbsoluteUri &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto));
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
        Insert = new RelayCommand(_ => Apply(false), _ => CanApply && editor.CanInsert);
        Replace = new RelayCommand(_ => Apply(true), _ => CanApply && editor.CanReplace);
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
        contextTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => { if(contextDirty) UpdateContextBudget(); }, dispatcher);
        if(inference is IAiRuntimeStatusSource runtimeSource) runtimeSource.RuntimeChanged += OnRuntimeChanged;
        Messages.CollectionChanged += OnMessagesChanged;
        store.Changed += OnSettingsChanged;
        editor.Changed += OnEditorChanged;
        keyReset.StateChanged += OnKeyStateChanged;
        LocalizationManager.CultureChanged += OnCultureChanged;
        Attachments.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasAttachments), nameof(CanAttach)); Refresh(); };
        UpdateContextBudget();
    }

    public ObservableCollection<AiChatMessage> Messages { get; } = [];
    public AiRuntimeMonitor Runtime { get; }
    public AiChatAppearance Appearance => store.Current.ChatAppearance;
    public int ContextLength => contextLength;
    public int RemainingTokens => remainingTokens;
    public int ReplyTokenLimit => replyTokenLimit;
    public double ContextUsagePercent => contextLength > 0 ? 100d * (contextLength - remainingTokens) / contextLength : 0;
    public bool IsContextLow => remainingTokens <= Math.Max(64, contextLength / 10);
    public string ContextSummary => LocalizationManager.Format(contextIsEstimate ? "AI.ContextEstimate" : "AI.ContextUsage",
        remainingTokens, contextLength, replyTokenLimit);
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
    public string Prompt
    {
        get => prompt;
        set { SetProperty(ref prompt, value); contextDirty = true; OnPropertyChanged(nameof(SendUnavailableReason)); Send.RaiseCanExecuteChanged(); }
    }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string SendUnavailableReason => !Enabled ? LocalizationManager.GetString("AI.DisabledHint")
        : IsLocked ? LocalizationManager.GetString("AI.SendLocked")
        : IsBusy ? LocalizationManager.GetString("AI.SendBusy")
        : isAttaching ? LocalizationManager.GetString("AI.SendAttaching")
        : string.IsNullOrWhiteSpace(Prompt) && !HasAttachments ? LocalizationManager.GetString("AI.SendEmpty") : string.Empty;
    public bool HasAttachments => Attachments.Count > 0;
    public bool CanAttach => !IsBusy && !isAttaching && !IsLocked && Attachments.Count < AiAttachmentService.MaxFiles;
    public string InsertUnavailableReason => IsBusy ? LocalizationManager.GetString("AI.SendBusy")
        : IsLocked ? LocalizationManager.GetString("AI.SendLocked")
        : !CanApply ? LocalizationManager.GetString("AI.ApplyNeedsAnswer")
        : !editor.CanInsert ? LocalizationManager.GetString("AI.ApplyNeedsEditor") : string.Empty;
    public string ReplaceUnavailableReason => InsertUnavailableReason.Length > 0 ? InsertUnavailableReason
        : !editor.CanReplace ? LocalizationManager.GetString("AI.ApplyNeedsSelection") : string.Empty;
    private bool IsLocked => WorkspaceLockPresentation.IsLocked(keyReset.State, keyReset.HasRetainedDocument);
    private bool CanApply => !IsBusy && !IsLocked && !string.IsNullOrWhiteSpace(lastResponse?.Content);
    public AsyncRelayCommand Send { get; }
    public ICommand Stop { get; }
    public ICommand Toggle { get; }
    public ICommand OpenPanel { get; }
    public ICommand OpenSettings { get; }
    public ICommand OpenAppearance { get; }
    public ICommand OpenHyperlink { get; }
    public AsyncRelayCommand AttachFiles { get; }
    public ICommand AttachDocument { get; }
    public ICommand RemoveAttachment { get; }
    public ICommand ClearChat { get; }
    public ICommand NewChat => ClearChat;
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
        Attachments.Clear();
        Prompt = string.Empty;
        var conversation = Messages.Where(m => m.Role != "assistant" || !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new AiTurn(m.Role, m.Content, m.Attachments)).ToList();
        conversation.Add(new("user", input, files));
        requestEstimate = AiContextBudget.Estimate(store.Current.SelectedModel, conversation);
        tokenUsage = null;
        Messages.Add(new("user", input, files));
        var answer = new AiChatMessage("assistant", string.Empty);
        Messages.Add(answer);
        lastResponse = answer;
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken);
        long version = ++operationVersion;
        var usageSource = inference as IAiTokenUsageSource;
        CancellationToken usageToken = requestCancellation.Token;
        void ReportUsage(AiTokenUsage usage) => OnTokenUsage(usage, version, usageToken);
        if(usageSource is not null) usageSource.TokenUsageChanged += ReportUsage;
        cancellation = requestCancellation;
        IsBusy = true;
        Status = LocalizationManager.GetString("AI.Loading");
        try
        {
            bool hasImages = conversation.Any(turn => turn.Attachments?.Any(file => file.IsImage) == true);
            var progress = new Progress<float>(value =>
            {
                if(!requestCancellation.IsCancellationRequested && !disposed && version == operationVersion)
                    Status = value >= 1 ? LocalizationManager.GetString(answer.HasReasoning && answer.Content.Length == 0 ? "AI.Thinking"
                        : hasImages && answer.Content.Length == 0 ? "AI.ProcessingImage" : "AI.Generating")
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
            }, progress, requestCancellation.Token, text =>
            {
                dispatcher.InvokeAsync(() =>
                {
                    if(!requestCancellation.IsCancellationRequested && !disposed && version == operationVersion)
                    {
                        answer.Reasoning += text;
                        if(answer.Content.Length == 0) Status = LocalizationManager.GetString("AI.Thinking");
                    }
                }, DispatcherPriority.Normal);
            });
            // Drain queued stream fragments before evaluating the final answer.
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            if(version == operationVersion)
                Status = LocalizationManager.GetString(string.IsNullOrWhiteSpace(answer.Content)
                    ? answer.HasReasoning ? "AI.ReasoningOnly" : "AI.EmptyAnswer" : "AI.Ready");
        }
        catch(AiRepetitionException error)
        {
            // Keep ordering with queued fragments, without cancelling their dispatcher callbacks.
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            if(version == operationVersion && !disposed)
            {
                var stop = error.Stop;
                string text = stop.IsReasoning ? answer.Reasoning : answer.Content;
                string useful = text[..Math.Min(text.Length, stop.KeepCharacters)];
                if(stop.IsReasoning) answer.Reasoning = useful;
                else answer.Content = useful;
                Status = LocalizationManager.GetString("AI.RepetitionStopped");
            }
        }
        catch(OperationCanceledException) { if(version == operationVersion) Status = LocalizationManager.GetString("AI.Stopped"); }
        catch(Exception e) { if(version == operationVersion) Status = ErrorText(e); }
        finally
        {
            if(usageSource is not null) usageSource.TokenUsageChanged -= ReportUsage;
            cancellation = null;
            if(string.IsNullOrWhiteSpace(answer.Content) && !answer.HasReasoning) Messages.Remove(answer);
            IsBusy = false;
        }
    }

    private void Apply(bool replace)
    {
        if(!CanApply || !editor.CanInsert || replace && !editor.CanReplace) return;
        // Capture the current cursor/selection immediately before this explicit user action.
        if(editor.Capture()?.Apply(lastResponse!.Content, replace) != true)
            Status = LocalizationManager.GetString("AI.DocumentChanged");
        else { Status = LocalizationManager.GetString("AI.Inserted"); Refresh(); }
    }

    private void CopyResponse()
    {
        try { System.Windows.Clipboard.SetText(lastResponse!.Content); }
        catch(System.Runtime.InteropServices.ExternalException) { Status = LocalizationManager.GetString("AI.CopyFailed"); }
    }

    private void Clear()
    {
        operationVersion++;
        tokenUsage = null; requestEstimate = 0; contextCorrection = 0;
        Messages.Clear(); lastResponse = null;
        AttachFiles.Cancel();
        Prompt = string.Empty; Attachments.Clear(); Status = string.Empty; Refresh();
        UpdateContextBudget();
    }
    private void Refresh()
    {
        contextDirty = true;
        OnPropertyChanged(nameof(IsModelLoaded), nameof(CanAttach), nameof(SendUnavailableReason),
            nameof(InsertUnavailableReason), nameof(ReplaceUnavailableReason));
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
        var previous = previousConfiguration;
        previousConfiguration = store.Current;
        OnPropertyChanged(nameof(Appearance));
        contextDirty = true;
        if(previous.Enabled == store.Current.Enabled && previous.SelectedModel == store.Current.SelectedModel)
            return;
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
        OnPropertyChanged(nameof(ModelName), nameof(SendUnavailableReason), nameof(ContextSummary),
            nameof(InsertUnavailableReason), nameof(ReplaceUnavailableReason));
        foreach(var message in Messages) message.OnPropertyChanged(nameof(AiChatMessage.RoleLabel));
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        void RefreshEditing()
        {
            if(disposed) return;
            (Insert as RelayCommand)?.RaiseCanExecuteChanged();
            (Replace as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(InsertUnavailableReason), nameof(ReplaceUnavailableReason));
        }
        if(dispatcher.CheckAccess()) RefreshEditing(); else _ = dispatcher.InvokeAsync(RefreshEditing);
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach(var message in observedMessages) message.PropertyChanged -= OnMessageChanged;
        observedMessages.Clear();
        foreach(var message in Messages)
            if(observedMessages.Add(message)) message.PropertyChanged += OnMessageChanged;
        contextDirty = true;
    }
    private void OnMessageChanged(object? sender, PropertyChangedEventArgs e) => contextDirty = true;
    private void OnRuntimeChanged(object? sender, EventArgs e) => _ = dispatcher.InvokeAsync(() => contextDirty = true);
    private void OnTokenUsage(AiTokenUsage usage, long version, CancellationToken requestToken)
    {
        _ = dispatcher.InvokeAsync(() =>
        {
            if(requestToken.IsCancellationRequested || disposed || version != operationVersion || !IsBusy) return;
            tokenUsage = usage;
            contextCorrection = usage.InputTokens - requestEstimate;
            contextDirty = true;
        });
    }
    internal void UpdateContextBudget()
    {
        contextDirty = false;
        var settings = store.Current.SelectedModel;
        var runtime = inference as IAiRuntimeStatusSource;
        contextLength = tokenUsage?.ContextLength ?? (runtime?.ModelPath == settings.ModelPath
            ? runtime.RuntimeOptions?.ContextLength : null) ?? settings.ContextLength;
        contextIsEstimate = !IsBusy || tokenUsage is null;
        // A cleared chat has no request yet: template/system overhead is counted on input/send.
        if(Messages.Count == 0 && string.IsNullOrWhiteSpace(Prompt) && !HasAttachments)
            remainingTokens = contextLength;
        else if(!contextIsEstimate) remainingTokens = tokenUsage!.RemainingTokens;
        else
        {
            var turns = Messages.Where(message => message.Role != "assistant" || !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new AiTurn(message.Role, message.Content, message.Attachments));
            string draft = string.IsNullOrWhiteSpace(Prompt) && HasAttachments
                ? LocalizationManager.GetString("AI.DefaultFilePrompt") : Prompt.Trim();
            if(!IsBusy) turns = turns.Append(new AiTurn("user", draft, Attachments.ToArray()));
            long used = (long)AiContextBudget.Estimate(settings, turns) + contextCorrection;
            remainingTokens = (int)Math.Clamp(contextLength - used - 1, 0, contextLength);
        }
        replyTokenLimit = IsBusy && tokenUsage is not null
            ? Math.Min(remainingTokens, Math.Max(0, settings.MaxTokens - tokenUsage.OutputTokens))
            : Math.Min(settings.MaxTokens, remainingTokens);
        OnPropertyChanged(nameof(ContextLength), nameof(RemainingTokens), nameof(ReplyTokenLimit),
            nameof(ContextUsagePercent), nameof(IsContextLow), nameof(ContextSummary));
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
        disposed = true; cancellation?.Cancel(); idleTimer.Stop(); contextTimer.Stop();
        if(inference is IAiRuntimeStatusSource runtimeSource) runtimeSource.RuntimeChanged -= OnRuntimeChanged;
        Messages.CollectionChanged -= OnMessagesChanged;
        foreach(var message in observedMessages) message.PropertyChanged -= OnMessageChanged;
        observedMessages.Clear();
        store.Changed -= OnSettingsChanged;
        editor.Changed -= OnEditorChanged;
        keyReset.StateChanged -= OnKeyStateChanged;
        LocalizationManager.CultureChanged -= OnCultureChanged;
        Runtime.Dispose();
        Clear();
    }
}
