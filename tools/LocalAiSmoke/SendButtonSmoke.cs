using Autofac;
using CryptoBook.AI;
using CryptoBook.FileTemplates;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.Security;
using CryptoBook.Views;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

internal static class SendButtonSmoke
{
    public static void Run(string model, string outputDirectory, bool checkReasoning = false)
    {
        Directory.CreateDirectory(outputDirectory);
        var report = new Dictionary<string, object?>();
        var app = UiPreview.CreateApplication();
        try
        {
            var store = new AiSettingsStore(Path.Combine(outputDirectory, "settings.json"));
            store.Save(new() { Enabled = true, SelectedModelPath = model,
                Models = [new() { ModelPath = model, ContextLength = 2048, MaxTokens = checkReasoning ? 768 : 32, Temperature = 0,
                    MemoryMode = checkReasoning ? AiMemoryMode.Manual : AiMemoryMode.Automatic, GpuLayers = 8 }] });
            var snapshot = new PendingSnapshot();
            using var container = new Startup().ConfigureServices(app, builder =>
            {
                builder.RegisterInstance(store);
                builder.RegisterInstance(new AiRuntimeProfileStore(Path.Combine(outputDirectory, "runtime-profiles.json")));
                builder.RegisterInstance(snapshot).As<ILockSnapshotService>();
            });
            var keyReset = container.Resolve<IKeyResetService>();
            var assistant = container.Resolve<AiAssistantViewModel>();
            var runtime = container.Resolve<IAiInferenceService>();
            var usageUpdates = new List<AiTokenUsage>();
            ((IAiTokenUsageSource)runtime).TokenUsageChanged += usage => { lock(usageUpdates) usageUpdates.Add(usage); };
            var panel = new AiAssistantPanel { DataContext = assistant };
            var host = new Window { Content = panel, Width = 500, Height = 900, ShowInTaskbar = false, ShowActivated = false };
            assistant.IsOpen = true;
            host.Show();
            var input = (TextBox)panel.FindName("PromptBox");
            var send = (Button)panel.FindName("SendButton");
            var hint = (TextBlock)panel.FindName("SendAvailabilityHint");
            PumpUntil(() => hint.Text == LocalizationManager.GetString("AI.SendEmpty"));
            Check(!send.IsEnabled, "Empty input must disable Send.");
            keyReset.Start();
            PumpUntil(() => keyReset.State == KeyResetState.KeyReset);
            Check(!keyReset.HasRetainedDocument, "The pending snapshot must not retain a decrypted workspace.");
            input.SetCurrentValue(TextBox.TextProperty, "Reply with one word: ready");
            PumpUntil(() => assistant.Prompt == input.Text && send.IsEnabled && hint.Visibility == Visibility.Collapsed);
            report["pending_snapshot_allows_send_after_typing"] = true;
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "send-enabled.png"));
            var peer = new ButtonAutomationPeer(send);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            PumpUntil(() => assistant.Send.IsRunning);
            Check(!send.IsEnabled, "Send must be disabled while the request runs.");
            PumpUntil(() => !assistant.Send.IsRunning);
            string answer = assistant.Messages.LastOrDefault(message => message.Role == "assistant")?.Content ?? "";
            Check(answer.Contains("ready", StringComparison.OrdinalIgnoreCase), "Unexpected answer: " + assistant.Status);
            report["real_button_invocation_generated_answer"] = answer;
            lock(usageUpdates)
            {
                Check(usageUpdates.Count >= 2 && usageUpdates[0].InputTokens > 0 && usageUpdates[0].OutputTokens == 0 &&
                    usageUpdates[^1].OutputTokens > 0 && usageUpdates[^1].RemainingTokens < usageUpdates[0].RemainingTokens,
                    "The native worker did not report prompt and generated token counts.");
                report["native_token_usage"] = usageUpdates.ToArray();
            }
            input.SetCurrentValue(TextBox.TextProperty, "next question");
            PumpUntil(() => send.IsEnabled && hint.Visibility == Visibility.Collapsed);
            report["send_reenabled_for_followup"] = true;
            UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "send-after-reply.png"));
            if(checkReasoning)
            {
                var modeResults = new List<object>();
                int modeIndex = 0;
                foreach(bool enabled in new[] { false, true, false })
                {
                    using var viewModel = new AiModelSettingsViewModel(store, runtime);
                    var settings = new AiModelSettingsWindow(viewModel);
                    settings.Show();
                    var checkbox = (CheckBox)settings.FindName("ReasoningCheckBox");
                    ((ScrollViewer)settings.FindName("SettingsScroll")).ScrollToBottom();
                    if(checkbox.IsChecked != enabled)
                    {
                        var toggle = new CheckBoxAutomationPeer(checkbox);
                        ((IToggleProvider)toggle.GetPattern(PatternInterface.Toggle)).Toggle();
                    }
                    UiPreview.CaptureWindow(settings, Path.Combine(outputDirectory, "reasoning-settings-" + modeIndex + ".png"));
                    var save = new ButtonAutomationPeer((Button)settings.FindName("SaveModelButton"));
                    ((IInvokeProvider)save.GetPattern(PatternInterface.Invoke)).Invoke();
                    PumpUntil(() => !settings.IsVisible && !runtime.IsLoaded);
                    Check(new AiSettingsStore(Path.Combine(outputDirectory, "settings.json")).Current.SelectedModel.UseReasoning == enabled,
                        "The checkbox value was not saved.");
                    input.SetCurrentValue(TextBox.TextProperty, enabled
                        ? "How many positive divisors does 360 have? Reply with only the count."
                        : "Сколько будет 17 + 25? Ответь только числом.");
                    PumpUntil(() => send.IsEnabled);
                    ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                    PumpUntil(() => assistant.Send.IsRunning);
                    PumpUntil(() => !assistant.Send.IsRunning);
                    var message = assistant.Messages.Last(message => message.Role == "assistant");
                    string reply = message.Content.Trim();
                    Check(reply == (enabled ? "24" : "42"), "Reasoning leaked into the answer or generation failed: " + reply + "; " + assistant.Status);
                    if(message.HasReasoning)
                    {
                        var block = Descendants(panel).OfType<Expander>().Single(element =>
                            element.Name == "ReasoningExpander" && ReferenceEquals(element.DataContext, message));
                        Check(block.IsVisible && !block.IsExpanded, "The reasoning block must be visible and initially collapsed.");
                        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "reasoning-collapsed.png"));
                        block.IsExpanded = true;
                        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "reasoning-expanded.png"));
                        report["real_reasoning_block_collapses_and_expands"] = true;
                    }
                    modeResults.Add(new { reasoning = enabled, saved = viewModel.UseReasoning, reply, analysis = message.Reasoning });
                    modeIndex++;
                }
                report["checkbox_off_on_off_with_real_replies"] = modeResults;
            }
            if(!checkReasoning)
            {
                VerifyEditorButtons(container, assistant, panel, host, outputDirectory);
                report["insert_and_replace_buttons_use_current_editor_and_selection"] = true;
                report["answer_can_be_inserted_repeatedly_and_changes_are_undoable"] = true;
            }
            var unloading = runtime.UnloadAsync();
            PumpUntil(() => unloading.IsCompleted); unloading.GetAwaiter().GetResult();
            report["unloaded"] = !runtime.IsLoaded;
            Check(snapshot.WriteAttempts == 0, "The test must not change the protected snapshot.");
            report["snapshot_untouched"] = true;
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

    private static void VerifyEditorButtons(IContainer container, AiAssistantViewModel assistant, AiAssistantPanel panel,
        Window host, string outputDirectory)
    {
        var editor = container.Resolve<IRichTextBoxService>();
        var session = container.Resolve<IDocumentSession>();
        const string original = "Исходный текст документа";
        var run = new Run(original);
        var document = new FlowDocument(new Paragraph(run));
        session.Open("ai-apply-smoke.txt", new PlainTextTemplate(), document);
        assistant.BindWorkspace(new EditorNavigation());
        host.Content = null;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        grid.Children.Add(editor.Service);
        Grid.SetColumn(panel, 1); grid.Children.Add(panel);
        host.Content = grid; host.Width = 1050;
        editor.Selection.Select(run.ContentStart, run.ContentStart);
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "apply-insert-enabled.png"));
        var insert = (Button)panel.FindName("InsertResponseButton");
        var replace = (Button)panel.FindName("ReplaceSelectionButton");
        Check(insert.IsEnabled && !replace.IsEnabled, "Insert did not become available after opening the editor: " + assistant.InsertUnavailableReason);
        editor.Selection.Select(run.ContentStart, run.ContentEnd);
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "apply-replace-enabled.png"));
        Check(replace.IsEnabled, "Selecting text after the answer did not enable Replace: " + assistant.ReplaceUnavailableReason);
        string answer = assistant.Messages.Last(message => message.Role == "assistant").Content;
        ((IInvokeProvider)new ButtonAutomationPeer(replace).GetPattern(PatternInterface.Invoke)).Invoke();
        PumpUntil(() => new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n') == answer);
        Check(insert.IsEnabled, "Applying a reply disabled further insertion.");
        editor.Undo();
        Check(new TextRange(document.ContentStart, document.ContentEnd).Text.Contains(original), "Undo did not restore the replaced selection.");
        var end = document.Blocks.LastBlock!.ContentEnd;
        editor.Selection.Select(end, end);
        ((IInvokeProvider)new ButtonAutomationPeer(insert).GetPattern(PatternInterface.Invoke)).Invoke();
        PumpUntil(() => new TextRange(document.ContentStart, document.ContentEnd).Text.Contains(original + answer));
        Check(insert.IsEnabled, "Insert became disabled after its first use.");
        ((IInvokeProvider)new ButtonAutomationPeer(insert).GetPattern(PatternInterface.Invoke)).Invoke();
        PumpUntil(() => new TextRange(document.ContentStart, document.ContentEnd).Text.Contains(original + answer + answer));
        UiPreview.CaptureWindow(host, Path.Combine(outputDirectory, "apply-inserted-twice.png"));
    }

    private sealed class EditorNavigation : IPageNavigationService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public Page? CurrentPage => null;
        public string? CurrentKey => "Home";
        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public IReadOnlyList<string>? Keys => ["Home"];
        public void Navigate(string key, object? args = null) { }
        public void GoBack() { }
        public void GoForward() { }
        public void Remove(string key) { }
    }

    private static void PumpUntil(Func<bool> completed)
    {
        var frame = new DispatcherFrame();
        DateTime deadline = DateTime.UtcNow.AddMinutes(3);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background,
            (_, _) => { if(completed() || DateTime.UtcNow >= deadline) frame.Continue = false; }, Dispatcher.CurrentDispatcher);
        Dispatcher.PushFrame(frame); timer.Stop();
        if(!completed()) throw new TimeoutException("Send button scenario did not complete.");
    }

    private static void Check(bool condition, string error)
    {
        if(!condition) throw new InvalidOperationException(error);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach(var descendant in Descendants(child)) yield return descendant;
        }
    }

    // Matches startup with an encrypted snapshot. Never accesses the user's snapshot or documents.
    private sealed class PendingSnapshot : ILockSnapshotService
    {
        public string SnapshotPath => "unused";
        public bool Exists => true;
        public int WriteAttempts { get; private set; }
        public Task CreateAndVerifyAsync(IRichTextBoxService richTextBox, LockSnapshotMetadata metadata,
            CancellationToken cancellationToken = default) { WriteAttempts++; throw new NotSupportedException(); }
        public Task<(FlowDocument Document, LockSnapshotMetadata Metadata)> ReadAndVerifyAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete() { WriteAttempts++; throw new NotSupportedException(); }
    }
}
