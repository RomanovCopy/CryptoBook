using CryptoBook.Infrastructure;
using Microsoft.Win32;
using System.IO;
using System.Windows.Input;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace CryptoBook.AI;

public sealed record AiMemoryOption(AiMemoryMode Value, string DisplayName);

public sealed class AiModelSettingsViewModel : ViewModelBase, IDisposable
{
    private readonly AiSettingsStore store;
    private readonly IAiInferenceService inference;
    private readonly Dictionary<string, AiModelSettings> profiles = new(StringComparer.OrdinalIgnoreCase);
    private string modelPath = string.Empty, status = string.Empty;
    private AiModelSettings parameters = new();
    private bool enabled, isBusy;
    private int idleMinutes;
    public event EventHandler? Saved;

    public AiModelSettingsViewModel(AiSettingsStore store, IAiInferenceService inference)
    {
        this.store = store; this.inference = inference;
        foreach(var profile in store.Current.Models) profiles[profile.ModelPath] = profile;
        enabled = store.Current.Enabled; idleMinutes = store.Current.IdleMinutes;
        ModelPath = store.Current.SelectedModelPath;
        Browse = new RelayCommand(_ => BrowseModel(), _ => !IsBusy);
        Save = new RelayCommand(_ => SaveSettings(), _ => !IsBusy);
        Test = new AsyncRelayCommand(async (_, token) =>
        {
            IsBusy = true;
            Status = LocalizationManager.GetString("AI.Loading");
            try
            {
                var profile = ValidatedParameters();
                var progress = new Progress<float>(v => Status = LocalizationManager.Format("AI.LoadProgress", (int)(v * 100)));
                await inference.GenerateAsync(profile with { MaxTokens = 128 },
                    [new("user", "Reply with one word: ready")], _ => { }, progress, token);
                Status = LocalizationManager.GetString("AI.ModelWorks");
            }
            catch(OperationCanceledException) { Status = LocalizationManager.GetString("AI.Stopped"); }
            catch(Exception e) { Status = AiAssistantViewModel.ErrorText(e); }
            finally { IsBusy = false; }
        }, _ => CanTest);
        StopTest = new RelayCommand(_ => Test.Cancel(), _ => IsBusy);
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
            OnPropertyChanged(nameof(CanTest));
            (Browse as RelayCommand)?.RaiseCanExecuteChanged();
            (Save as RelayCommand)?.RaiseCanExecuteChanged();
            (StopTest as RelayCommand)?.RaiseCanExecuteChanged();
            Test?.RaiseCanExecuteChanged();
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
                nameof(GpuLayers), nameof(UseReasoning), nameof(SystemPrompt), nameof(RecentModels), nameof(SelectedRecentModel),
                nameof(MemoryMode), nameof(IsManualMemoryMode), nameof(MemoryDescription));
            Test?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanTest));
        }
    }
    public int ContextLength { get => parameters.ContextLength; set { parameters = parameters with { ContextLength = value }; OnPropertyChanged(); } }
    public int MaxTokens { get => parameters.MaxTokens; set { parameters = parameters with { MaxTokens = value }; OnPropertyChanged(); } }
    public float Temperature { get => parameters.Temperature; set { parameters = parameters with { Temperature = value }; OnPropertyChanged(); } }
    public int GpuLayers { get => parameters.GpuLayers; set { parameters = parameters with { GpuLayers = value }; OnPropertyChanged(); } }
    public IReadOnlyList<AiMemoryOption> MemoryModes => Enum.GetValues<AiMemoryMode>()
        .Select(mode => new AiMemoryOption(mode, LocalizationManager.GetString("AI.MemoryMode." + mode))).ToArray();
    public AiMemoryMode MemoryMode
    {
        get => parameters.MemoryMode;
        set
        {
            if(value == parameters.MemoryMode) return;
            parameters = parameters with { MemoryMode = value };
            OnPropertyChanged(nameof(MemoryMode), nameof(IsManualMemoryMode), nameof(MemoryDescription));
        }
    }
    public bool IsManualMemoryMode => MemoryMode == AiMemoryMode.Manual;
    public string MemoryDescription => LocalizationManager.GetString("AI.MemoryHint." + MemoryMode);
    public bool UseReasoning { get => parameters.UseReasoning; set { parameters = parameters with { UseReasoning = value }; OnPropertyChanged(); } }
    public string SystemPrompt { get => parameters.SystemPrompt; set { parameters = parameters with { SystemPrompt = value }; OnPropertyChanged(); } }
    public int IdleMinutes { get => idleMinutes; set => SetProperty(ref idleMinutes, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool CanTest => !IsBusy && !string.IsNullOrWhiteSpace(ModelPath);
    public ICommand Browse { get; }
    public ICommand Save { get; }
    public AsyncRelayCommand Test { get; }
    public ICommand StopTest { get; }

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
            store.Save(new() { Enabled = Enabled, SelectedModelPath = ModelPath, IdleMinutes = IdleMinutes, Models = merged.Values.ToList() });
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch(Exception e) { Status = e is AiException ? AiAssistantViewModel.ErrorText(e) : LocalizationManager.GetString("AI.SaveFailed"); }
    }
    private void OnCultureChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(MemoryModes), nameof(MemoryDescription));
    public void Dispose()
    {
        LocalizationManager.CultureChanged -= OnCultureChanged;
        Test.Cancel();
    }
}
