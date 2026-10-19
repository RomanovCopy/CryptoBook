using CryptoBook.Infrastructure;
using System.Windows.Threading;

namespace CryptoBook.AI;

/// <summary>Localized presentation of runtime decisions; contains no model request content.</summary>
public sealed class AiRuntimeMonitor : ViewModelBase, IDisposable
{
    private readonly IAiRuntimeStatusSource? source;
    private readonly Dispatcher dispatcher;
    private bool disposed;
    public AiRuntimeMonitor(IAiInferenceService inference)
    {
        source = inference as IAiRuntimeStatusSource;
        dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if(source is not null) source.RuntimeChanged += OnChanged;
        LocalizationManager.CultureChanged += OnChanged;
    }
    public bool HasInfo => source?.Notices.Count > 0;
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
        if(!disposed) OnPropertyChanged(nameof(HasInfo), nameof(Stage), nameof(Summary), nameof(Details), nameof(Warnings), nameof(ModelPath), nameof(ModelName));
    }
    public void Dispose()
    {
        disposed = true;
        if(source is not null) source.RuntimeChanged -= OnChanged;
        LocalizationManager.CultureChanged -= OnChanged;
    }
}
