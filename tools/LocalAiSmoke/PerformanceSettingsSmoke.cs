using CryptoBook.AI;
using CryptoBook.Views;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class PerformanceSettingsSmoke
{
    internal static void Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var app = UiPreview.CreateApplication();
        string model = Path.Combine(Path.GetFullPath(outputDirectory), "fixture.gguf");
        File.WriteAllBytes(model, "GGUF"u8.ToArray());
        var store = new AiSettingsStore(Path.Combine(outputDirectory, "settings.json"));
        store.Save(new() { Enabled = true, SelectedModelPath = model, Models = [new() { ModelPath = model }] });
        using var runtime = new AdaptiveAiInferenceService(new(Path.Combine(outputDirectory, "profiles.json")),
            () => new Worker(), () => new(16L << 30, 12L << 30, 20L << 30, true));
        using var viewModel = new AiModelSettingsViewModel(store, runtime);
        var window = new AiModelSettingsWindow(viewModel);
        window.Show(); window.UpdateLayout();
        var mode = (ComboBox)window.FindName("PerformanceModeBox");
        var onlyAc = (CheckBox)window.FindName("BoostOnlyOnAcCheckBox");
        var test = (Button)window.FindName("PerformanceTestButton");
        Check((AiPerformanceMode)mode.SelectedValue == AiPerformanceMode.Balanced && !test.IsEnabled, "Must default to balanced mode.");
        mode.SelectedValue = AiPerformanceMode.PowerSaving;
        PumpUntil(() => viewModel.PerformanceMode == AiPerformanceMode.PowerSaving);
        Check(!test.IsEnabled && !onlyAc.IsEnabled, "Power saving must not enable calibration or boost controls.");
        mode.SelectedValue = AiPerformanceMode.Optimal;
        PumpUntil(() => test.IsEnabled);
        Check(store.Current.SelectedModel.EffectivePerformanceMode == AiPerformanceMode.Balanced, "Draft mode was saved automatically.");
        Invoke(test);
        PumpUntil(() => viewModel.IsBusy);
        PumpUntil(() => !viewModel.PerformanceTest.IsRunning);
        Check(runtime.GetLastPerformance(store.Current.SelectedModel)?.BoostSelected == true, "No measured profile was saved.");
        Check(store.Current.SelectedModel.EffectivePerformanceMode == AiPerformanceMode.Balanced, "Testing silently changed the saved mode.");
        ((FrameworkElement)test.Parent).BringIntoView();
        UiPreview.CaptureWindow(window, Path.Combine(outputDirectory, "performance-light.png"));
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/DarkTheme.xaml", UriKind.Relative) });
        UiPreview.CaptureWindow(window, Path.Combine(outputDirectory, "performance-dark.png"));
        window.Width = window.MinWidth; window.Height = window.MinHeight;
        test.BringIntoView();
        UiPreview.CaptureWindow(window, Path.Combine(outputDirectory, "performance-small.png"));
        // Cancel a second run through the same command bound to the global Stop button.
        Invoke(test);
        PumpUntil(() => viewModel.IsBusy);
        viewModel.StopTest.Execute(null);
        PumpUntil(() => !viewModel.PerformanceTest.IsRunning);
        Check(!viewModel.IsBusy && !runtime.IsBusy, "Stop left the settings busy.");
        Invoke((Button)window.FindName("SaveModelButton"));
        PumpUntil(() => !window.IsVisible);
        var restored = new AiSettingsStore(Path.Combine(outputDirectory, "settings.json"));
        Check(restored.Current.SelectedModel.EffectivePerformanceMode == AiPerformanceMode.Optimal && restored.Current.SelectedModel.BoostOnlyOnAc,
            "Saved mode or power constraint was lost.");
        using var reopened = new AiModelSettingsViewModel(restored, runtime);
        Check(reopened.PerformanceMode == AiPerformanceMode.Optimal, "Saved mode is not shown after reopening.");
        Check(reopened.PerformanceSummary.Contains("Обычный режим"), "Persisted result is not shown after reopening.");
        File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(new
        {
            passed = true, uses_native_model = false, balanced_default = true, power_saving_controls = true,
            test_does_not_save_mode = true, cancellation_passed = true, saved_and_reopened = true,
            note = "Real WPF controls and commands; deterministic benchmark worker, no user settings or model required."
        }, new JsonSerializerOptions { WriteIndented = true }));
        app.Shutdown();
        Console.WriteLine("AI performance settings UI passed: " + outputDirectory);
    }
    private static void Invoke(Button button)
    {
        Check(button.IsEnabled, "Target button is disabled.");
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    }
    private static void PumpUntil(Func<bool> completed)
    {
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background,
            (_, _) => { if(completed() || DateTime.UtcNow > deadline) frame.Continue = false; }, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame); timer.Stop();
        Check(completed(), "UI operation timed out.");
    }
    private static void Check(bool value, string error) { if(!value) throw new Exception(error); }
    private sealed class Worker : IAiWorkerSession
    {
        public bool IsAlive { get; private set; } = true;
        public async Task<AiModelInspection?> ExecuteAsync(AiWorkerRequest request, Action<string> onText,
            IProgress<float>? progress, CancellationToken cancellationToken, Action<string>? onReasoning = null,
            Action<AiTokenUsage>? onUsage = null, Action<AiPerformanceMeasurement>? onMeasurement = null)
        {
            await Task.Delay(80, cancellationToken);
            if(request.Command == "inspect") return new(new(4, 8, 500_000_000, 200_000_000, 300_000_000,
                2L << 30, 12L << 30, "Fixture GPU"), new(), "fixture-hardware");
            if(request.Command == "benchmark") onMeasurement?.Invoke(new(200, 64, 1,
                request.Options.PerformanceBoost ? 1 : 2, 1024L << 20, 512L << 20));
            return null;
        }
        public void Stop() => IsAlive = false;
        public void Dispose() => Stop();
    }
}
