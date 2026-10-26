using CryptoBook.Infrastructure;
using System.Windows.Threading;

namespace CryptoBook.AI;

/// <summary>Localized presentation of runtime decisions; contains no model request content.</summary>
public sealed class AiRuntimeMonitor : ViewModelBase, IDisposable
{
    private readonly IAiInferenceService inference;
    private readonly IAiRuntimeStatusSource? source;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer modelStatusTimer;
    private string lastModelStatusKey;
    private bool disposed;
    public AiRuntimeMonitor(IAiInferenceService inference)
    {
        this.inference = inference;
        source = inference as IAiRuntimeStatusSource;
        dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        lastModelStatusKey = ModelStatusKey;
        // An idle native worker can exit without a request producing a runtime event.
        modelStatusTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => RefreshModelStatus(), dispatcher);
        if(source is not null) source.RuntimeChanged += OnChanged;
        LocalizationManager.CultureChanged += OnChanged;
    }
    public bool HasInfo => source?.Notices.Count > 0;
    private string ModelStatusKey => inference.IsLoaded ? "AI.ModelStatus.Loaded"
        : inference.IsBusy ? "AI.ModelStatus.Loading" : "AI.ModelStatus.Unloaded";
    public string ModelStatus => LocalizationManager.GetString(ModelStatusKey);
    public string ModelPath => source?.ModelPath ?? "";
    public string ModelName => System.IO.Path.GetFileNameWithoutExtension(ModelPath);
    public string Stage => source?.Notices.LastOrDefault() is { } notice ? Format(notice) : "";
    public string Summary
    {
        get
        {
            if(source?.RuntimeOptions is not { } options) return "";
            string summary = LocalizationManager.Format("AI.AutoSummary", options.ContextLength, options.GpuLayers,
                options.BatchSize, options.UBatchSize, options.FlashAttention == false ? "off" : "auto");
            summary += "\n" + LocalizationManager.Format("AI.RuntimePerformance",
                LocalizationManager.GetString("AI.PerformanceMode." + (options.PowerSaving ? AiPerformanceMode.PowerSaving :
                    options.PerformanceBoost ? AiPerformanceMode.Optimal : AiPerformanceMode.Balanced)),
                options.Threads > 0 ? options.Threads : Math.Max(1, Environment.ProcessorCount / 2),
                options.BatchThreads > 0 ? options.BatchThreads : Math.Max(1, Environment.ProcessorCount / 2));
            if(source.MemoryPlan is { } plan)
                summary += "\n" + LocalizationManager.Format("AI.AutoResources", plan.AvailableRamBytes / (1024 * 1024),
                    plan.AvailableGpuBytes / (1024 * 1024), plan.GpuName);
            return summary;
        }
    }
    public string Details => string.Join(Environment.NewLine, source?.Notices.Select(notice =>
        $"{notice.Time.ToLocalTime():HH:mm:ss}  {Format(notice)}") ?? []);
    public string Warnings
    {
        get
        {
            if(source?.RuntimeOptions is not { } options) return "";
            var warnings = new List<string>();
            if(options.ContextLength < source.RequestedContextLength)
                warnings.Add(LocalizationManager.Format("AI.AutoReducedContext", source.RequestedContextLength, options.ContextLength));
            if(options.GpuLayers == 0) warnings.Add(LocalizationManager.GetString("AI.AutoCpuSelected"));
            if(options.FlashAttention == false) warnings.Add(LocalizationManager.GetString("AI.AutoFlashDisabled"));
            if(source.Notices.Any(notice => notice.ResourceKey == "AI.AutoPartialReply"))
                warnings.Add(LocalizationManager.GetString("AI.AutoPartialReply"));
            return string.Join(Environment.NewLine, warnings);
        }
    }
    private static string Format(AiRuntimeNotice notice)
    {
        var arguments = notice.Arguments.Select(value => value is string key && key.StartsWith("AI.", StringComparison.Ordinal)
            ? LocalizationManager.GetString(key) : value).ToArray();
        return LocalizationManager.Format(notice.ResourceKey, arguments);
    }
    private void OnChanged(object? sender, EventArgs e)
    {
        if(disposed) return;
        if(dispatcher.CheckAccess()) Refresh();
        else _ = dispatcher.InvokeAsync(Refresh, DispatcherPriority.Normal);
    }
    private void Refresh()
    {
        if(disposed) return;
        lastModelStatusKey = ModelStatusKey;
        OnPropertyChanged(nameof(ModelStatus), nameof(HasInfo), nameof(Stage), nameof(Summary), nameof(Details), nameof(Warnings), nameof(ModelPath), nameof(ModelName));
    }
    internal void RefreshModelStatus()
    {
        if(disposed || ModelStatusKey == lastModelStatusKey) return;
        lastModelStatusKey = ModelStatusKey;
        OnPropertyChanged(nameof(ModelStatus));
    }
    public void Dispose()
    {
        disposed = true;
        modelStatusTimer.Stop();
        if(source is not null) source.RuntimeChanged -= OnChanged;
        LocalizationManager.CultureChanged -= OnChanged;
    }
}
