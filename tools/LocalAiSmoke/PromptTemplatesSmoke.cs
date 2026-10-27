using Autofac;
using CryptoBook.AI;
using CryptoBook.Injections;
using CryptoBook.Infrastructure;
using CryptoBook.MyControls;
using CryptoBook.Views;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;

internal static class PromptTemplatesSmoke
{
    public static void Run(string outputDirectory, string? referenceSettingsPath = null)
    {
        Directory.CreateDirectory(outputDirectory);
        var app = UiPreview.CreateApplication();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        var report = new Dictionary<string, object?>();
        void Check(bool condition, string name)
        {
            if(!condition) throw new InvalidOperationException(name);
            report[name] = true;
        }
        try
        {
            string path = Path.Combine(outputDirectory, "settings.json");
            string firstModel = Path.Combine(outputDirectory, "first.gguf"), secondModel = Path.Combine(outputDirectory, "second.gguf");
            File.WriteAllBytes(firstModel, "GGUF"u8.ToArray());
            File.WriteAllBytes(secondModel, "GGUF"u8.ToArray());
            var store = new AiSettingsStore(path);
            if(referenceSettingsPath is not null)
            {
                var reference = JsonSerializer.Deserialize<AiConfiguration>(File.ReadAllText(referenceSettingsPath))!;
                Check(store.Current.PromptTemplates.SequenceEqual(reference.PromptTemplates), "bundled_presets_match_saved_names_and_prompts_exactly");
            }
            int presetCount = store.Current.PromptTemplates.Count;
            report["bundled_preset_count"] = presetCount;
            store.Save(new() { Enabled = true, SelectedModelPath = firstModel, Models =
                [new() { ModelPath = firstModel }, new() { ModelPath = secondModel }] });
            using var runtime = new RecordingRuntime();
            using var container = new Startup().ConfigureServices(app, builder =>
            {
                builder.RegisterInstance(store);
                builder.RegisterInstance(runtime).As<IAiInferenceService>();
            });
            var assistant = container.Resolve<AiAssistantViewModel>();
            assistant.IsOpen = true;
            assistant.Messages.Add(new("user", "Сохрани смысл и исправь ошибки в документе."));
            assistant.Messages.Add(new("assistant", "Пришлите текст для редактирования."));
            assistant.Prompt = "Следующий вопрос";
            var panel = new AiAssistantPanel { DataContext = assistant };
            var host = new Window { Content = panel, Width = 430, Height = 900, ShowInTaskbar = false, ShowActivated = false };
            host.Show(); Pump();
            var selector = (ComboBox)panel.FindName("PromptTemplateBox");
            Check(selector.Items.Count == presetCount && selector.SelectedItem is AiPromptTemplateViewModel { Id: "default" }, "panel_initial_binding");
            assistant.SelectedPromptTemplateId = "concise"; Pump();
            Check(store.Current.SelectedPromptTemplateId == "concise" && assistant.Messages.Count == 2 && assistant.Prompt == "Следующий вопрос", "panel_selection_preserves_chat_and_draft");
            Check(runtime.UnloadCount == 0, "selection_keeps_weights");

            string customId;
            using(var draft = new AiModelSettingsViewModel(store, runtime))
            {
                var window = new AiModelSettingsWindow(draft) { ShowInTaskbar = false, ShowActivated = false };
                window.Show(); Pump();
                var templates = (ComboBox)window.FindName("PromptTemplatesBox");
                var presetPrompt = (TextBox)window.FindName("SystemPromptBox");
                Check(presetPrompt.Text == store.Current.SelectedModel.SystemPrompt && !string.IsNullOrWhiteSpace(presetPrompt.Text), "bundled_prompt_visible_in_editor");
                presetPrompt.BringIntoView(); Pump();
                UiPreview.CaptureWindow(window, Path.Combine(outputDirectory, "bundled-template-editor-light.png"));
                Invoke((Button)window.FindName("AddPromptTemplateButton"));
                var name = (TextBox)window.FindName("PromptTemplateNameBox");
                var prompt = (TextBox)window.FindName("SystemPromptBox");
                name.Text = "Мой редактор документов";
                prompt.Text = "Исправляй ошибки, сохраняя смысл и стиль автора.\nВозвращай только исправленный текст.";
                Pump();
                customId = draft.Templates.SelectedTemplate!.Id;
                Check(draft.Templates.SelectedTemplate.Name == name.Text && draft.Templates.SelectedTemplate.SystemPrompt == prompt.Text,
                    "editor_fields_update_draft");
                Check(store.Current.PromptTemplates.Count == presetCount, "editing_does_not_save_implicitly");
                templates.SelectedIndex = 0; Pump();
                templates.SelectedItem = draft.Templates.Items.Single(template => template.Id == customId); Pump();
                Check(name.Text == "Мой редактор документов" && prompt.Text.StartsWith("Исправляй ошибки"), "switching_templates_keeps_edits");
                ((ComboBox)window.FindName("RecentModelsBox")).SelectedItem = secondModel; Pump();
                ((ComboBox)window.FindName("RecentModelsBox")).GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource(); Pump();
                Check(draft.ModelPath == secondModel && name.Text == "Мой редактор документов", "switching_model_keeps_global_template");
                name.BringIntoView(); Pump();
                UiPreview.CaptureWindow(window, Path.Combine(outputDirectory, "template-editor-light.png"));
                Invoke((Button)window.FindName("SaveModelButton"));
                Check(!window.IsVisible && store.Current.SelectedPromptTemplateId == customId, "save_closes_and_persists_selection");
            }
            Pump();
            Check(selector.Items.Count == presetCount + 1 && (selector.SelectedItem as AiPromptTemplateViewModel)?.Id == customId, "panel_refreshes_after_settings_save");
            Check(selector.SelectedIndex >= 0 && selector.SelectionBoxItem is AiPromptTemplateViewModel { Name: "Мой редактор документов" },
                "saved_template_is_rendered_in_selection_box");
            assistant.Messages.Add(new("assistant", "Готов работать по вашему шаблону."));
            assistant.Prompt = "Проверь документ";
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "template-panel-light.png"));
            using(var edit = new AiModelSettingsViewModel(store, runtime))
            {
                var window = new AiModelSettingsWindow(edit) { ShowInTaskbar = false, ShowActivated = false };
                window.Show(); Pump();
                ((TextBox)window.FindName("PromptTemplateNameBox")).Text = "Редактор с пояснениями";
                ((TextBox)window.FindName("SystemPromptBox")).Text = "Исправляй ошибки и кратко объясняй каждое исправление.";
                Invoke((Button)window.FindName("SaveModelButton"));
                Check(store.Current.SelectedPromptTemplate!.Name == "Редактор с пояснениями", "existing_template_rename_saved");
                Check(store.Current.SelectedModel.SystemPrompt.Contains("объясняй"), "existing_template_prompt_edit_saved");
            }
            using(var cancelled = new AiModelSettingsViewModel(store, runtime))
            {
                var window = new AiModelSettingsWindow(cancelled) { ShowInTaskbar = false, ShowActivated = false };
                window.Show(); Pump();
                ((TextBox)window.FindName("SystemPromptBox")).Text = "Не сохранять";
                Invoke((Button)window.FindName("RemovePromptTemplateButton"));
                window.Close(); Pump();
                Check(store.Current.SelectedPromptTemplateId == customId && store.Current.SelectedModel.SystemPrompt.Contains("объясняй"), "cancel_discards_edits_and_deletion");
            }
            using(var removal = new AiModelSettingsViewModel(store, runtime))
            {
                var window = new AiModelSettingsWindow(removal) { ShowInTaskbar = false, ShowActivated = false };
                window.Show(); Pump();
                Invoke((Button)window.FindName("AddPromptTemplateButton"));
                Invoke((Button)window.FindName("RemovePromptTemplateButton"));
                ((ComboBox)window.FindName("PromptTemplatesBox")).SelectedItem = removal.Templates.Items.Single(template => template.Id == customId);
                Invoke((Button)window.FindName("SaveModelButton"));
                Check(store.Current.PromptTemplates.Count == presetCount + 1, "create_and_delete_template");
            }
            var reloaded = new AiSettingsStore(path);
            Check(reloaded.Current.SelectedPromptTemplateId == customId && reloaded.Current.SelectedModel.SystemPrompt.Contains("объясняй"), "restart_restores_edits_and_selection");
            foreach(string model in new[] { firstModel, secondModel })
            {
                store.Save(store.Current with { SelectedModelPath = model }); Pump();
                assistant.Prompt = "Проверь: Это тестовый текст.";
                var sending = assistant.Send.ExecuteAsync();
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                while(!sending.IsCompleted && DateTime.UtcNow < deadline) Pump();
                Check(sending.IsCompleted, "generation_completes_" + Path.GetFileNameWithoutExtension(model));
                sending.GetAwaiter().GetResult();
                Check(runtime.LastSettings?.SystemPrompt == store.Current.SelectedPromptTemplate!.SystemPrompt,
                    "generation_receives_global_prompt_" + Path.GetFileNameWithoutExtension(model));
            }
            int unloadsBeforeReply = runtime.UnloadCount;
            runtime.WaitForStop = true;
            assistant.Prompt = "Покажи длинный ответ для проверки остановки.";
            Invoke((Button)panel.FindName("SendButton"));
            WaitUntil(() => assistant.IsBusy && assistant.Messages.Last().HasContent);
            string partialAnswer = assistant.Messages.Last().Content;
            Invoke((Button)panel.FindName("StopButton"));
            WaitUntil(() => !assistant.IsBusy && !assistant.Send.IsRunning);
            Check(assistant.Status == LocalizationManager.GetString("AI.Stopped") && assistant.IsModelLoaded,
                "stop_button_keeps_model_loaded");
            Check(assistant.Messages.Last().Content == partialAnswer && runtime.UnloadCount == unloadsBeforeReply,
                "stop_button_keeps_partial_answer_without_unload");
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "stopped-model-loaded-light.png"));
            runtime.WaitForStop = false;
            assistant.Prompt = "Продолжи диалог после остановки.";
            Invoke((Button)panel.FindName("SendButton"));
            WaitUntil(() => !assistant.IsBusy && !assistant.Send.IsRunning);
            Check(assistant.IsModelLoaded && runtime.UnloadCount == unloadsBeforeReply, "normal_completion_keeps_model_loaded");
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "completed-model-loaded-light.png"));
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri("/CryptoBook;component/Themes/DarkTheme.xaml", UriKind.Relative) });
            panel.Width = 280; Pump();
            host.UpdateLayout();
            Check(selector.SelectedItem is AiPromptTemplateViewModel { Name: "Редактор с пояснениями" } selected && selected.Id == customId,
                "selected_template_remains_visible_after_generation");
            Check(selector.ActualWidth > 100 && selector.IsVisible && selector.IsEnabled, "narrow_dark_selector_visible");
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "template-panel-narrow-dark.png"));
            Invoke((Button)panel.FindName("UnloadModelButton"));
            WaitUntil(() => !assistant.IsModelLoaded);
            Check(runtime.UnloadCount == unloadsBeforeReply + 1, "explicit_unload_button_releases_model");
            host.Close();
            report["success"] = true;
        }
        catch(Exception error)
        {
            report["success"] = false;
            report["error"] = error.ToString();
            Environment.ExitCode = 1;
        }
        finally
        {
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            app.Shutdown();
        }
    }

    private static void Invoke(Button button)
    {
        if(!button.IsEnabled) throw new InvalidOperationException("Disabled button: " + button.Name);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        Pump();
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void WaitUntil(Func<bool> ready)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while(!ready() && DateTime.UtcNow < deadline) Pump();
        if(!ready()) throw new TimeoutException("The WPF lifecycle operation did not finish.");
    }

    private sealed class RecordingRuntime : IAiInferenceService
    {
        public bool IsLoaded { get; private set; } = true;
        public bool IsBusy { get; private set; }
        public bool WaitForStop { get; set; }
        public DateTime LastUsedUtc => DateTime.UtcNow;
        public int UnloadCount { get; private set; }
        public AiModelSettings? LastSettings { get; private set; }
        public async Task GenerateAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation, Action<string> onText,
            IProgress<float>? loadProgress, CancellationToken cancellationToken, Action<string>? onReasoning = null)
        {
            LastSettings = settings;
            IsLoaded = IsBusy = true;
            try
            {
                onText("Исправленный текст: Это тестовый текст.");
                if(WaitForStop) await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally { IsBusy = false; }
        }
        public Task LoadAsync(AiModelSettings settings, IProgress<float>? loadProgress, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResetContextAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnloadAsync(CancellationToken cancellationToken = default) { UnloadCount++; IsLoaded = false; return Task.CompletedTask; }
        public void Dispose() { }
    }
}
