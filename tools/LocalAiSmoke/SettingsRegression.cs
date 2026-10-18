using Autofac;
using CryptoBook.AI;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.Views;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class SettingsRegression
{
    public static void Run(string firstModel, string secondModel, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var report = new Dictionary<string, object?> { ["first_model"] = firstModel, ["second_model"] = secondModel };
        var app = UiPreview.CreateApplication();
        try
        {
            using var container = new Startup().ConfigureServices(app);
            var runtime = container.Resolve<IAiInferenceService>();
            var store = new AiSettingsStore(Path.Combine(outputDirectory, "settings.json"));
            store.Save(new() { Enabled = true, SelectedModelPath = firstModel, Models =
                [new() { ModelPath = firstModel }, new() { ModelPath = secondModel }] });
            using(var settingsVm = new AiModelSettingsViewModel(store, runtime))
            {
                var settings = new AiModelSettingsWindow(settingsVm);
                settings.Show();
                ((TextBox)settings.FindName("TokensBox")).Text = "10000";
                Invoke((Button)settings.FindName("TestModelButton"));
                PumpUntil(() => !settingsVm.Test.IsRunning && settingsVm.Status == LocalizationManager.GetString("AI.ModelWorks"),
                    () => settingsVm.Status);
                Check(settingsVm.ContextLength == 4096 && settingsVm.MaxTokens == 10000, "Test did not commit the typed values.");
                report["test_with_4096_context_and_10000_reply"] = true;
                Invoke((Button)settings.FindName("SaveModelButton"));
                PumpUntil(() => !settings.IsVisible, () => settingsVm.Status);
                Check(store.Current.SelectedModel.MaxTokens == 10000, "Save lost the requested reply limit.");
                report["save_after_successful_test"] = true;
                report["saved_context"] = store.Current.SelectedModel.ContextLength;
                report["saved_reply_limit"] = store.Current.SelectedModel.MaxTokens;
            }

            using var assistant = new AiAssistantViewModel(store, runtime, container.Resolve<AiEditorContext>(),
                new AiSettingsWindowService(store, runtime), container.Resolve<IKeyResetService>(), Dispatcher.CurrentDispatcher);
            var panel = new AiAssistantPanel { DataContext = assistant };
            Grid.SetColumn(panel, 2);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            grid.Children.Add(panel);
            var host = new Window { Content = grid, Width = 1000, Height = 800, ShowInTaskbar = false };
            assistant.IsOpen = true;
            host.Show();
            SelectInSettings(store, runtime, secondModel);
            report["switch_to_second_model_unloaded_previous_model"] = true;
            report["second_model_answer"] = Ask(assistant);
            SelectInSettings(store, runtime, firstModel);
            report["switch_back_to_first_model"] = true;
            Check(store.Current.SelectedModel.MaxTokens == 10000, "Switching models lost the saved reply limit.");
            report["first_model_answer_with_10000_limit"] = Ask(assistant);
            var unloading = runtime.UnloadAsync();
            PumpUntil(() => unloading.IsCompleted); unloading.GetAwaiter().GetResult();
            report["unloaded"] = !runtime.IsLoaded;

            using(var invalidVm = new AiModelSettingsViewModel(store, runtime))
            {
                var invalid = new AiModelSettingsWindow(invalidVm);
                invalid.Show();
                ((TextBox)invalid.FindName("ContextBox")).Text = "0";
                Invoke((Button)invalid.FindName("TestModelButton"));
                PumpUntil(() => invalidVm.Status == LocalizationManager.GetString("AI.InvalidContextLength"));
                Invoke((Button)invalid.FindName("SaveModelButton"));
                PumpUntil(() => invalidVm.Status == LocalizationManager.GetString("AI.InvalidContextLength"));
                Check(invalid.IsVisible && !runtime.IsLoaded, "Invalid context was saved or started inference.");
                report["invalid_context_rejected_by_test_and_save"] = true;
                ((TextBox)invalid.FindName("ContextBox")).Text = "4096";
                ((TextBox)invalid.FindName("TokensBox")).Text = "abc";
                Invoke((Button)invalid.FindName("SaveModelButton"));
                PumpUntil(() => invalidVm.Status == LocalizationManager.GetString("AI.InvalidNumber"));
                Check(invalid.IsVisible && store.Current.SelectedModel.MaxTokens == 10000, "Invalid text changed the saved profile.");
                report["invalid_numeric_text_rejected"] = true;
                ((TextBox)invalid.FindName("TokensBox")).Text = "10000";
                Invoke((Button)invalid.FindName("SaveModelButton"));
                PumpUntil(() => !invalid.IsVisible, () => invalidVm.Status);
                report["corrected_input_saves"] = true;
            }
            host.Close();
            report["passed"] = true;
        }
        catch(Exception error)
        {
            report["passed"] = false;
            report["error"] = error.ToString();
            Environment.ExitCode = 1;
        }
        finally
        {
            foreach(Window window in app.Windows.OfType<Window>().ToArray()) window.Close();
            app.Shutdown();
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), json);
            Console.WriteLine(json);
        }
    }

    private static void SelectInSettings(AiSettingsStore store, IAiInferenceService runtime, string model)
    {
        using var viewModel = new AiModelSettingsViewModel(store, runtime);
        var settings = new AiModelSettingsWindow(viewModel);
        settings.Show();
        ((ComboBox)settings.FindName("RecentModelsBox")).SelectedItem = model;
        Invoke((Button)settings.FindName("SaveModelButton"));
        PumpUntil(() => !settings.IsVisible && store.Current.SelectedModelPath == model && !runtime.IsLoaded,
            () => viewModel.Status);
    }

    private static string Ask(AiAssistantViewModel assistant)
    {
        assistant.Prompt = "Reply with one word: ready";
        var sending = assistant.Send.ExecuteAsync();
        PumpUntil(() => sending.IsCompleted, () => assistant.Status);
        sending.GetAwaiter().GetResult();
        string answer = assistant.Messages.LastOrDefault(message => message.Role == "assistant")?.Content ?? "";
        Check(answer.Contains("ready", StringComparison.OrdinalIgnoreCase), "No expected model response: " + assistant.Status + "; " + answer);
        return answer;
    }

    private static void Invoke(Button button)
    {
        Check(button.IsEnabled, "The target button is disabled.");
        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }

    private static void PumpUntil(Func<bool> completed, Func<string>? status = null)
    {
        var frame = new DispatcherFrame();
        DateTime deadline = DateTime.UtcNow.AddMinutes(3);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background,
            (_, _) => { if(completed() || DateTime.UtcNow >= deadline) frame.Continue = false; }, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame); timer.Stop();
        if(!completed()) throw new TimeoutException("UI operation did not complete: " + status?.Invoke());
    }

    private static void Check(bool condition, string error)
    {
        if(!condition) throw new Exception(error);
    }
}
