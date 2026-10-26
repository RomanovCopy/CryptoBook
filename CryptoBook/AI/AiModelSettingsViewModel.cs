using CryptoBook.Infrastructure;
using Microsoft.Win32;
using System.IO;
using System.Windows.Input;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace CryptoBook.AI;

public sealed record AiMemoryOption(AiMemoryMode Value, string DisplayName);
public sealed record AiPerformanceOption(AiPerformanceMode Value, string DisplayName);

public sealed class AiModelSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly AiSettingsStore store;
    private readonly IAiInferenceService inference;
    private readonly Dictionary<string, AiModelSettings> profiles = new(StringComparer.OrdinalIgnoreCase);
    private string modelPath = string.Empty, status = string.Empty;
    private AiModelSettings parameters = new();
    private bool enabled, isBusy;
    private int idleMinutes;
    private readonly System.Windows.Threading.Dispatcher dispatcher = System.Windows.Application.Current?.Dispatcher
        ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private AiPerformanceResult? performanceResult;
    public event EventHandler? Saved;

    public AiModelSettingsViewModel(AiSettingsStore store, IAiInferenceService inference)
    {
        this.store = store; this.inference = inference;
        Runtime = new(inference);
        foreach(var profile in store.Current.Models) profiles[profile.ModelPath] = profile;
        enabled = store.Current.Enabled; idleMinutes = store.Current.IdleMinutes;
        ModelPath = store.Current.SelectedModelPath;
        Browse = new RelayCommand(_ => BrowseModel(), _ => !IsBusy);
        Save = new RelayCommand(_ => SaveSettings(), _ => !IsBusy);
        Test = new AsyncRelayCommand((_, token) => RunTestAsync(false, token), _ => CanTest);
        Retune = new AsyncRelayCommand((_, token) => RunTestAsync(true, token), _ => CanRetune);
        PerformanceTest = new AsyncRelayCommand((_, token) => RunPerformanceTestAsync(token), _ => CanPerformanceTest);
        StopTest = new RelayCommand(_ => { Test.Cancel(); Retune.Cancel(); PerformanceTest.Cancel(); }, _ => IsBusy);
        if(inference is IAiRuntimeStatusSource source) source.RuntimeChanged += OnRuntimeChanged;
        LocalizationManager.CultureChanged += OnCultureChanged;
    }

    public IReadOnlyList<string> RecentModels => profiles.Keys.ToArray();
    public string? SelectedRecentModel
    {
        get => profiles.ContainsKey(ModelPath) ? ModelPath : null;
        set { if(!string.IsNullOrWhiteSpace(value)) ModelPath = value; }
    }
    public bool Enabled { get => enabled; set => SetProperty(ref enabled, value); }
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            SetProperty(ref isBusy, value);
            OnPropertyChanged(nameof(CanTest), nameof(CanEdit), nameof(CanRetune), nameof(CanPerformanceTest));
            (Browse as RelayCommand)?.RaiseCanExecuteChanged();
            (Save as RelayCommand)?.RaiseCanExecuteChanged();
            (StopTest as RelayCommand)?.RaiseCanExecuteChanged();
            Test?.RaiseCanExecuteChanged();
            Retune?.RaiseCanExecuteChanged();
            PerformanceTest?.RaiseCanExecuteChanged();
        }
    }
    public string ModelPath
    {
        get => modelPath;
        set
        {
            value = value?.Trim() ?? string.Empty;
            if(modelPath == value) return;
            if(!string.IsNullOrWhiteSpace(modelPath)) profiles[modelPath] = CurrentParameters();
            modelPath = value;
            parameters = profiles.TryGetValue(value, out var profile) ? profile : new() { ModelPath = value };
            OnPropertyChanged(nameof(ModelPath), nameof(ContextLength), nameof(MaxTokens), nameof(Temperature),
                nameof(GpuLayers), nameof(UseReasoning), nameof(RepetitionProtection), nameof(ReuseContext), nameof(PreloadOnPanelOpen), nameof(SystemPrompt), nameof(RecentModels), nameof(SelectedRecentModel),
                nameof(MemoryMode), nameof(IsManualMemoryMode), nameof(IsAutomaticMemoryMode), nameof(AutoReduceContext), nameof(MemoryDescription),
                nameof(PerformanceMode), nameof(PerformanceDescription), nameof(IsOptimalPerformanceMode),
                nameof(BoostOnlyOnAc), nameof(CanPerformanceTest));
            RefreshPerformance();
            Test?.RaiseCanExecuteChanged();
            Retune?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanTest), nameof(CanRetune));
            PerformanceTest?.RaiseCanExecuteChanged();
        }
    }
    public int ContextLength { get => parameters.ContextLength; set { parameters = parameters with { ContextLength = value }; OnPropertyChanged(); RefreshPerformance(); } }
    public int MaxTokens { get => parameters.MaxTokens; set { parameters = parameters with { MaxTokens = value }; OnPropertyChanged(); } }
    public float Temperature { get => parameters.Temperature; set { parameters = parameters with { Temperature = value }; OnPropertyChanged(); } }
    public int GpuLayers { get => parameters.GpuLayers; set { parameters = parameters with { GpuLayers = value }; OnPropertyChanged(); RefreshPerformance(); } }
    public IReadOnlyList<AiMemoryOption> MemoryModes => Enum.GetValues<AiMemoryMode>()
        .Select(mode => new AiMemoryOption(mode, LocalizationManager.GetString("AI.MemoryMode." + mode))).ToArray();
    public AiMemoryMode MemoryMode
    {
        get => parameters.MemoryMode;
        set
        {
            if(value == parameters.MemoryMode) return;
            parameters = parameters with { MemoryMode = value };
            OnPropertyChanged(nameof(MemoryMode), nameof(IsManualMemoryMode), nameof(IsAutomaticMemoryMode), nameof(MemoryDescription), nameof(CanRetune));
            Retune?.RaiseCanExecuteChanged();
            RefreshPerformance();
        }
    }
    public bool IsManualMemoryMode => MemoryMode == AiMemoryMode.Manual;
    public bool IsAutomaticMemoryMode => MemoryMode == AiMemoryMode.Automatic;
    public bool AutoReduceContext { get => parameters.AutoReduceContext; set { parameters = parameters with { AutoReduceContext = value }; OnPropertyChanged(); } }
    public IReadOnlyList<AiPerformanceOption> PerformanceModes => Enum.GetValues<AiPerformanceMode>()
        .Select(mode => new AiPerformanceOption(mode, LocalizationManager.GetString("AI.PerformanceMode." + mode))).ToArray();
    public AiPerformanceMode PerformanceMode
    {
        get => parameters.EffectivePerformanceMode;
        set
        {
            if(value == PerformanceMode) return;
            parameters = parameters with { PerformanceMode = value, AllowPerformanceBoost = value == AiPerformanceMode.Optimal };
            OnPropertyChanged(nameof(PerformanceMode), nameof(PerformanceDescription), nameof(IsOptimalPerformanceMode), nameof(CanPerformanceTest));
            PerformanceTest?.RaiseCanExecuteChanged();
        }
    }
    public bool IsOptimalPerformanceMode => PerformanceMode == AiPerformanceMode.Optimal;
    public string PerformanceDescription => LocalizationManager.GetString("AI.PerformanceModeHint." + PerformanceMode);
    public bool BoostOnlyOnAc { get => parameters.BoostOnlyOnAc; set { parameters = parameters with { BoostOnlyOnAc = value }; OnPropertyChanged(); } }
    public bool CanPerformanceTest => CanTest && IsOptimalPerformanceMode && inference is IAiPerformanceService;
    public string PerformanceSummary => performanceResult is { } result ? LocalizationManager.Format("AI.PerformanceResult",
        result.TestedUtc.ToLocalTime(), result.Baseline.PromptTokensPerSecond, result.Baseline.GenerationTokensPerSecond,
        result.Selected.PromptTokensPerSecond, result.Selected.GenerationTokensPerSecond,
        Math.Max(result.Baseline.PeakRamBytes, result.Selected.PeakRamBytes) / (1024 * 1024),
        Math.Max(result.Baseline.PeakGpuBytes, result.Selected.PeakGpuBytes) / (1024 * 1024),
        result.BoostSelected ? LocalizationManager.Format("AI.PerformanceGain", (result.Speedup - 1) * 100)
            : LocalizationManager.GetString("AI.PerformanceNoGain")) : LocalizationManager.GetString("AI.PerformanceNotTested");
    public bool CanEdit => !IsBusy;
    public bool CanRetune => CanTest && IsAutomaticMemoryMode && inference is IAiRuntimeStatusSource;
    public AiRuntimeMonitor Runtime { get; }
    public string MemoryDescription => LocalizationManager.GetString("AI.MemoryHint." + MemoryMode);
    public bool UseReasoning { get => parameters.UseReasoning; set { parameters = parameters with { UseReasoning = value }; OnPropertyChanged(); } }
    public bool RepetitionProtection { get => parameters.RepetitionProtection; set { parameters = parameters with { RepetitionProtection = value }; OnPropertyChanged(); } }
    public bool ReuseContext { get => parameters.ReuseContext; set { parameters = parameters with { ReuseContext = value }; OnPropertyChanged(); } }
    public bool PreloadOnPanelOpen { get => parameters.PreloadOnPanelOpen; set { parameters = parameters with { PreloadOnPanelOpen = value }; OnPropertyChanged(); } }
    public string SystemPrompt { get => parameters.SystemPrompt; set { parameters = parameters with { SystemPrompt = value }; OnPropertyChanged(); } }
    public int IdleMinutes { get => idleMinutes; set => SetProperty(ref idleMinutes, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool CanTest => !IsBusy && !inference.IsBusy && !string.IsNullOrWhiteSpace(ModelPath);
    public ICommand Browse { get; }
    public ICommand Save { get; }
    public AsyncRelayCommand Test { get; }
    public AsyncRelayCommand Retune { get; }
    public AsyncRelayCommand PerformanceTest { get; }
    public ICommand StopTest { get; }

    private void RefreshPerformance()
    {
        performanceResult = (inference as IAiPerformanceService)?.GetLastPerformance(CurrentParameters());
        OnPropertyChanged(nameof(PerformanceSummary));
    }
    private void OnRuntimeChanged(object? sender, EventArgs e)
    {
        void RefreshCommands()
        {
            OnPropertyChanged(nameof(CanTest), nameof(CanRetune), nameof(CanPerformanceTest));
            Test?.RaiseCanExecuteChanged(); Retune?.RaiseCanExecuteChanged(); PerformanceTest?.RaiseCanExecuteChanged();
        }
        if(dispatcher.CheckAccess()) RefreshCommands();
        else _ = dispatcher.InvokeAsync(RefreshCommands);
    }
    private async Task RunPerformanceTestAsync(CancellationToken token)
    {
        if(inference is not IAiPerformanceService service) return;
        IsBusy = true;
        Status = LocalizationManager.GetString("AI.PerformancePreparing");
        try
        {
            performanceResult = await service.TestPerformanceAsync(ValidatedParameters(), token);
            OnPropertyChanged(nameof(PerformanceSummary));
            Status = LocalizationManager.GetString("AI.PerformanceDone");
        }
        catch(OperationCanceledException) { Status = LocalizationManager.GetString("AI.Stopped"); }
        catch(Exception error) { Status = AiAssistantViewModel.ErrorText(error); }
        finally { IsBusy = false; }
    }

    private async Task RunTestAsync(bool retune, CancellationToken token)
    {
        IsBusy = true;
        Status = LocalizationManager.GetString("AI.Loading");
        try
        {
            var profile = ValidatedParameters();
            if(retune && inference is IAiRuntimeStatusSource source)
                await source.ResetAutoProfileAsync(profile.ModelPath, token);
            var progress = new Progress<float>(value =>
            {
                if(!token.IsCancellationRequested && IsBusy)
                    Status = value >= 1 ? LocalizationManager.GetString("AI.Generating")
                        : LocalizationManager.Format("AI.LoadProgress", (int)(value * 100));
            });
            bool hasAnswer = false, hasReasoning = false;
            await inference.GenerateAsync(profile with { MaxTokens = 128 },
                [new("user", "Reply with one word: ready")], text => hasAnswer |= !string.IsNullOrWhiteSpace(text), progress, token,
                text => hasReasoning |= !string.IsNullOrWhiteSpace(text));
            Status = LocalizationManager.GetString(hasAnswer ? "AI.ModelWorks" : hasReasoning ? "AI.TestReasoningOnly" : "AI.EmptyAnswer");
        }
        catch(OperationCanceledException) { Status = LocalizationManager.GetString("AI.Stopped"); }
        catch(Exception error) { Status = AiAssistantViewModel.ErrorText(error); }
        finally { IsBusy = false; }
    }

    private AiModelSettings CurrentParameters() => parameters with { ModelPath = ModelPath };
    private AiModelSettings ValidatedParameters()
    {
        var profile = CurrentParameters();
        if(Enabled || !string.IsNullOrWhiteSpace(ModelPath)) profile.Validate();
        if(IdleMinutes is < 0 or > 120) throw new AiException("AI.InvalidIdleMinutes");
        return profile;
    }
    internal void ReportInvalidInput() => Status = LocalizationManager.GetString("AI.InvalidNumber");
    private void BrowseModel()
    {
        var picker = new OpenFileDialog
        {
            Filter = LocalizationManager.GetString("AI.ModelFilter"),
            Title = LocalizationManager.GetString("AI.SelectModel"),
            CheckFileExists = true,
        };
        if(File.Exists(ModelPath)) picker.InitialDirectory = Path.GetDirectoryName(ModelPath);
        if(picker.ShowDialog() == true) ModelPath = picker.FileName;
    }
    private void SaveSettings()
    {
        try
        {
            var profile = ValidatedParameters();
            if(!string.IsNullOrWhiteSpace(ModelPath)) profiles[ModelPath] = profile;
            var merged = store.Current.Models.ToDictionary(model => model.ModelPath, StringComparer.OrdinalIgnoreCase);
            foreach(var draft in profiles.Values) merged[draft.ModelPath] = draft;
            store.Save(store.Current with { Enabled = Enabled, SelectedModelPath = ModelPath, IdleMinutes = IdleMinutes, Models = merged.Values.ToList() });
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch(Exception e) { Status = e is AiException ? AiAssistantViewModel.ErrorText(e) : LocalizationManager.GetString("AI.SaveFailed"); }
    }
    private void OnCultureChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(MemoryModes), nameof(MemoryDescription),
        nameof(PerformanceModes), nameof(PerformanceDescription), nameof(PerformanceSummary));
    public void Dispose()
    {
        LocalizationManager.CultureChanged -= OnCultureChanged;
        Test.Cancel();
        Retune.Cancel();
        PerformanceTest.Cancel();
        if(inference is IAiRuntimeStatusSource source) source.RuntimeChanged -= OnRuntimeChanged;
        Runtime.Dispose();
    }
}
