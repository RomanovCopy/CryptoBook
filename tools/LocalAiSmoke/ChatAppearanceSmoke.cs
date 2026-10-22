using Autofac;
using CryptoBook.AI;
using CryptoBook.Injections;
using CryptoBook.MyControls;
using CryptoBook.Views;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

internal static class ChatAppearanceSmoke
{
    public static void Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var app = UiPreview.CreateApplication();
        var report = new Dictionary<string, object?>();
        try
        {
            var store = new AiSettingsStore(Path.Combine(outputDirectory, "settings.json"));
            store.Save(new());
            using var runtime = new IdleRuntime();
            using var container = new Startup().ConfigureServices(app, builder =>
            {
                builder.RegisterInstance(store);
                builder.RegisterInstance(runtime).As<IAiInferenceService>();
            });
            var assistant = container.Resolve<AiAssistantViewModel>();
            assistant.IsOpen = true;
            assistant.Messages.Add(new("user", "Как изменить оформление чата?"));
            assistant.Messages.Add(new("assistant", "Откройте «Оформление чата», выберите шрифт, размер и цвета сообщений."));
            assistant.Prompt = "Следующий вопрос";
            assistant.Attachments.Add(AiAttachmentService.Document("Черновик.txt", "Тестовое вложение"));
            var panel = new AiAssistantPanel { DataContext = assistant };
            var host = new Window { Content = panel, Width = 420, Height = 900, ShowInTaskbar = false, ShowActivated = false };
            host.Show();
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-default-light.png"));
            Check(MessageBorders(panel).All(border => border.BorderThickness == new Thickness(0)), "Default messages have frames.");
            var appearance = new AiChatAppearanceViewModel(store);
            var settings = new AiChatAppearanceWindow(appearance) { ShowInTaskbar = false, ShowActivated = false };
            settings.Show();
            ((ComboBox)settings.FindName("FontBox")).SelectedItem = "Consolas";
            ((ComboBox)settings.FindName("FontSizeBox")).SelectedItem = 20d;
            ((TextBox)settings.FindName("UserColorBox")).Text = "#1267B0";
            ((TextBox)settings.FindName("AssistantColorBox")).Text = "#208050";
            appearance.ShowMessageBorders = true;
            UiPreview.CaptureWindow(settings, Path.Combine(outputDirectory, "appearance-light.png"));
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri("/CryptoBook;component/Themes/DarkTheme.xaml", UriKind.Relative) });
            UiPreview.CaptureWindow(settings, Path.Combine(outputDirectory, "appearance-dark.png"));
            settings.Width = settings.MinWidth; settings.Height = settings.MinHeight;
            UiPreview.CaptureWindow(settings, Path.Combine(outputDirectory, "appearance-small-dark.png"));
            var save = (Button)settings.FindName("SaveAppearanceButton");
            ((IInvokeProvider)new ButtonAutomationPeer(save).GetPattern(PatternInterface.Invoke)).Invoke();
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-custom-dark.png"));
            Check(!settings.IsVisible, "Appearance settings did not save.");
            Check(assistant.Messages.Count == 2 && assistant.Prompt == "Следующий вопрос" && assistant.Attachments.Count == 1,
                "Appearance save lost conversation state.");
            Check(runtime.UnloadCount == 0, "Appearance save unloaded the model.");
            var messages = Descendants(panel).OfType<RichTextBox>().Where(box => box.DataContext is AiChatMessage && box.Name == "MessageContent").ToArray();
            Check(messages.Length == 2, "Message templates were not created.");
            foreach(var box in messages)
            {
                Check(box.FontFamily.Source == "Consolas" && box.FontSize == 20, "Message font binding failed.");
                Check(box.Document.FontFamily.Source == "Consolas" && box.Document.FontSize == 20, "Rendered message font binding failed.");
                string expected = ((AiChatMessage)box.DataContext).Role == "user" ? "#FF1267B0" : "#FF208050";
                Check(box.Foreground.ToString() == expected, "Message color binding failed: " + box.Foreground);
                Check(box.Document.Foreground.ToString() == expected, "Rendered message color binding failed.");
            }
            Check(MessageBorders(panel).All(border => border.BorderThickness == new Thickness(1)), "Message border toggle failed.");
            var input = (TextBox)panel.FindName("PromptBox");
            Check(input.FontFamily.Source == "Consolas" && input.FontSize == 20, "Prompt font binding failed.");
            Check(new AiSettingsStore(Path.Combine(outputDirectory, "settings.json")).Current.ChatAppearance == store.Current.ChatAppearance,
                "Appearance persistence failed.");
            store.Save(store.Current with { ChatAppearance = store.Current.ChatAppearance with { ShowMessageBorders = false } });
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-custom-borderless-dark.png"));
            Check(MessageBorders(panel).All(border => border.BorderThickness == new Thickness(0)), "Removing message borders failed.");
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri("/CryptoBook;component/Themes/LightTheme.xaml", UriKind.Relative) });
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-custom-borderless-light.png"));
            VerifyMarkdown(app, host, panel, assistant, store, outputDirectory);
            VerifyMessageSelection(app, host, panel, assistant, outputDirectory);
            foreach(string name in new[] { "ClearChatButton", "NewChatButton" })
            {
                assistant.Messages.Add(new("user", new string('Ж', 5000)));
                assistant.Prompt = "Черновик прошлого чата";
                assistant.UpdateContextBudget();
                Check(assistant.ContextUsagePercent > 0, "The current chat did not consume any context.");
                var reset = (Button)panel.FindName(name);
                Check(reset.IsVisible && reset.IsEnabled, "The chat reset button is unavailable: " + name);
                ((IInvokeProvider)new ButtonAutomationPeer(reset).GetPattern(PatternInterface.Invoke)).Invoke();
                UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-reset-" + name + ".png"));
                Check(assistant.Messages.Count == 0 && assistant.Prompt.Length == 0 && assistant.Attachments.Count == 0,
                    "Chat reset left previous content: " + name);
                Check(assistant.RemainingTokens == assistant.ContextLength && assistant.ContextUsagePercent == 0 && !assistant.IsContextLow,
                    "Chat reset did not restore the full context budget: " + name);
                Check(assistant.SelectedMessageCount == 0 && !assistant.Copy.CanExecute(null), "Chat reset left selected messages: " + name);
            }
            host.Close();
            report["passed"] = true;
            report["checked"] = "WPF controls, font/color bindings, borders on/off, light/dark themes, minimum settings window size, persistence, Markdown checkbox on/off with source and token preservation, message checkbox bindings, selection counter, select/clear all, disabled empty messages, selection layout at narrow width, conversation preservation and both chat reset buttons restoring the full token budget and clearing selection";
        }
        catch(Exception error)
        {
            report["passed"] = false; report["error"] = error.ToString(); Environment.ExitCode = 1;
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

    private static void VerifyMarkdown(Application app, Window host, AiAssistantPanel panel, AiAssistantViewModel assistant,
        AiSettingsStore store, string outputDirectory)
    {
        const string source = "# План работы\n\n**Готово:** настройки чата.\n\n- Шрифт и цвета\n- Счётчик токенов\n\n| Режим | Вид |\n|---|---|\n| Markdown | Оформленный |\n\n```csharp\nint count = 42;\n```";
        assistant.Messages[1].Content = source;
        host.Height = 1100;
        store.Save(store.Current with { ChatAppearance = new() { RenderMarkdown = true } });
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-markdown-light.png"));
        var viewer = Descendants(panel).OfType<RichTextBox>().Single(box => ReferenceEquals(box.DataContext, assistant.Messages[1]));
        Check(viewer.Document.Blocks.OfType<Table>().Any(), "Markdown table was not rendered.");
        Check(!Read(viewer).Contains("**"), "Markdown emphasis was not rendered.");
        assistant.UpdateContextBudget();
        int tokens = assistant.RemainingTokens;
        foreach(bool enabled in new[] { false, true })
        {
            var settingsVm = new AiChatAppearanceViewModel(store);
            var window = new AiChatAppearanceWindow(settingsVm) { ShowInTaskbar = false, ShowActivated = false };
            window.Show();
            var checkbox = (CheckBox)window.FindName("RenderMarkdownCheckBox");
            Check(checkbox.IsChecked == !enabled, "The Markdown checkbox did not restore the saved value.");
            checkbox.BringIntoView();
            ((IToggleProvider)new CheckBoxAutomationPeer(checkbox).GetPattern(PatternInterface.Toggle)).Toggle();
            UiPreview.CaptureWindow(window, Path.Combine(outputDirectory, "appearance-markdown-" + enabled + ".png"));
            var save = (Button)window.FindName("SaveAppearanceButton");
            ((IInvokeProvider)new ButtonAutomationPeer(save).GetPattern(PatternInterface.Invoke)).Invoke();
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-markdown-" + enabled + ".png"));
            Check(!window.IsVisible && new AiSettingsStore(Path.Combine(outputDirectory, "settings.json")).Current.ChatAppearance.RenderMarkdown == enabled,
                "The Markdown choice was not persisted.");
            Check(Read(viewer).Contains("**") != enabled, "Switching Markdown did not update the current message.");
            assistant.UpdateContextBudget();
            Check(assistant.Messages[1].Content == source && assistant.RemainingTokens == tokens,
                "Markdown rendering changed the source message or token budget.");
        }
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/DarkTheme.xaml", UriKind.Relative) });
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-markdown-dark.png"));
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/LightTheme.xaml", UriKind.Relative) });
        host.Height = 900;
    }

    private static void VerifyMessageSelection(Application app, Window host, AiAssistantPanel panel,
        AiAssistantViewModel assistant, string outputDirectory)
    {
        assistant.Messages[1].Reasoning = "Рассуждения не переносятся";
        assistant.Messages.Add(new("user", "Можно перенести только этот вопрос и предыдущий ответ?"));
        assistant.Messages.Add(new("assistant", "Да. Отметьте нужные сообщения флажками и нажмите «Вставить выбранное»."));
        assistant.Messages.Add(new("assistant", "") { Reasoning = "Только рассуждения" });
        host.Height = 1000;
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-selection-empty.png"));
        var checkboxes = Descendants(panel).OfType<CheckBox>().Where(box => box.Name == "SelectMessageCheckBox").ToArray();
        Check(checkboxes.Length == 5 && !checkboxes[4].IsEnabled, "Empty messages should not be selectable.");
        foreach(int index in new[] { 2, 1 })
            ((IToggleProvider)new CheckBoxAutomationPeer(checkboxes[index]).GetPattern(PatternInterface.Toggle)).Toggle();
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-selection-light.png"));
        Check(assistant.SelectedMessageCount == 2 && assistant.Messages[1].IsSelected && assistant.Messages[2].IsSelected,
            "Message checkbox bindings did not update the selection.");
        var counter = (TextBlock)panel.FindName("MessageSelectionCounter");
        Check(counter.Text == assistant.MessageSelectionSummary, "The selection counter binding failed.");
        Check(assistant.GetSelectedText().IndexOf("# План работы", StringComparison.Ordinal) <
            assistant.GetSelectedText().IndexOf("Можно перенести", StringComparison.Ordinal), "Selected messages are out of order.");
        Check(!assistant.GetSelectedText().Contains("Рассуждения"), "Reasoning was included in the selected text.");
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/DarkTheme.xaml", UriKind.Relative) });
        panel.Width = 280;
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-selection-narrow-dark.png"));
        foreach(string name in new[] { "SelectAllMessagesButton", "ClearMessageSelectionButton" })
        {
            var button = (Button)panel.FindName(name);
            Check(button.IsVisible && button.IsEnabled, "Selection button is unavailable: " + name);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "chat-selection-" + name + ".png"));
            Check(assistant.SelectedMessageCount == (name == "SelectAllMessagesButton" ? 4 : 0), "Bulk selection failed: " + name);
        }
        Check(checkboxes.All(box => box.IsChecked == false), "Clearing selection did not update the checkboxes.");
        Check(!assistant.Copy.CanExecute(null), "Copy should be disabled without selected messages.");
        panel.Width = 390; host.Height = 900;
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/CryptoBook;component/Themes/LightTheme.xaml", UriKind.Relative) });
    }

    private static string Read(RichTextBox viewer) => new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;

    private static IEnumerable<Border> MessageBorders(DependencyObject panel) => Descendants(panel).OfType<Border>()
        .Where(border => border.DataContext is AiChatMessage && border.Child is StackPanel);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach(var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Check(bool condition, string message)
    {
        if(!condition) throw new InvalidOperationException(message);
    }
    private sealed class IdleRuntime : IAiInferenceService
    {
        public int UnloadCount { get; private set; }
        public bool IsLoaded => true;
        public bool IsBusy => false;
        public DateTime LastUsedUtc => DateTime.UtcNow;
        public Task GenerateAsync(AiModelSettings settings, IReadOnlyList<AiTurn> conversation, Action<string> onText,
            IProgress<float>? loadProgress, CancellationToken cancellationToken, Action<string>? onReasoning = null) => throw new NotSupportedException();
        public Task UnloadAsync(CancellationToken cancellationToken = default) { UnloadCount++; return Task.CompletedTask; }
        public void Dispose() { }
    }
}
