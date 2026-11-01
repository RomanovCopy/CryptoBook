using Autofac;
using CryptoBook.AI;
using CryptoBook.DTO;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

internal static partial class Program
{
    private static readonly string[] Stage93Topics = ["ai.context", "ai.memory", "ai.performance", "ai.appearance"];

    private static async Task ValidateStage93(IsolatedServices isolated, bool verify, bool performance, bool reasoningOnly)
    {
        var c = isolated.Container;
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.Width = 1440; main.Height = 960; main.WindowState = WindowState.Normal;
        if (verify)
        {
            c.Resolve<IHelpWindowService>().Open(); await Settle(main);
            var help = Application.Current.Windows.OfType<HelpWindow>().Single();
            var vm = (HelpWindowViewModel)help.DataContext;
            var content = c.Resolve<HelpContentService>();
            foreach (string id in Stage93Topics)
            {
                Check(vm.OpenTopic(id), $"Embedded stage9.3 topic opens: {id}."); await Settle(help);
                var article = content.Find(culture, id)!;
                Check(article.Images.Any() && article.Images.All(path => content.LoadImage(culture, article, path) is not null), $"Embedded stage9.3 images load: {id}.");
            }
            help.Close(); return;
        }
        string modelPath = Environment.GetEnvironmentVariable("CRYPTOBOOK_HELP_MODEL") ?? throw new InvalidOperationException("Set CRYPTOBOOK_HELP_MODEL.");
        var model = new AiModelSettings { ModelPath = modelPath, ContextLength = 1024, MaxTokens = 96, Temperature = 0 };
        model.Validate();
        var store = c.Resolve<AiSettingsStore>();
        var ai = c.Resolve<AiAssistantViewModel>();
        var engine = c.Resolve<IAiInferenceService>();
        var runtime = (IAiRuntimeStatusSource)engine;
        string file = Path.Combine(isolated.DemoDirectory, "Settings-demo.txt");
        File.WriteAllText(file, Local("Вымышленный план библиотеки.", "Вигаданий план бібліотеки.", "Fiktiver Bibliotheksplan.", "Fictional library plan."));
        demonstrationFile = file;
        Check((await c.Resolve<IWorkspaceFileOpenService>().OpenAsync(file)).Success, "Disposable document opens for the isolated AI settings checks.");
        var template = new AiPromptTemplate { Name = Local("Учебный помощник", "Навчальний помічник", "Testassistent", "Practice assistant"), SystemPrompt = Local("Отвечай по-русски, кратко и точно.", "Відповідай українською, стисло й точно.", "Antworte kurz und genau auf Deutsch.", "Reply briefly and accurately in English.") };
        store.Save(store.Current with { Enabled = true, SelectedModelPath = modelPath, Models = [model], PromptTemplates = [template], SelectedPromptTemplateId = template.Id });
        ai.IsOpen = true; await Settle(main);
        var panel = Named<CryptoBook.MyControls.AiAssistantPanel>(main, "AiPanel");
        Named<Thumb>(main, "ResizeHandle").RaiseEvent(new DragDeltaEventArgs(-180, 0) { RoutedEvent = Thumb.DragDeltaEvent }); await Settle(main);
        var host = new Stage93Host(main, panel, ai, store, engine);
        var resourceType = typeof(AiModelSettings).Assembly.GetType("CryptoBook.AI.AiResourceSnapshot")!;
        object resources = resourceType.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        bool onAc = (bool)resourceType.GetProperty("OnAc")!.GetValue(resources)!;
        Console.WriteLine($"STAGE93 culture={culture} performance={performance} onAc={onAc}");
        if (reasoningOnly)
        {
            await host.Change((d, _) => { d.ContextLength = 2048; d.MaxTokens = 512; d.UseReasoning = true; });
            var reply = await CaptureStage93Reasoning(isolated, host);
            WriteJson(Path.GetRelativePath(repo, Path.Combine(isolated.Root, "stage9-reasoning.json")), new { culture, nativeReasoningProvided = reply.HasReasoning, reasoning = reply.Reasoning, reasoningAnswer = reply.Content });
        }
        else if (performance) await ValidateStage93Performance(isolated, host, runtime, onAc);
        else await ValidateStage93Settings(isolated, host, runtime);
        await ai.Unload.ExecuteAsync();
    }

    private sealed class Stage93Host(MainWindow main, CryptoBook.MyControls.AiAssistantPanel panel, AiAssistantViewModel ai, AiSettingsStore store, IAiInferenceService engine)
    {
        public MainWindow Main { get; } = main;
        public AiAssistantViewModel Ai { get; } = ai;
        public AiSettingsStore Store { get; } = store;
        public IAiInferenceService Engine { get; } = engine;
        public void PanelShot(string id, string topic, string state) => Shot(Main, id, topic, state, [], Bounds(Main, panel));
        public AiModelSettingsWindow Settings()
        {
            Ai.OpenSettings.Execute(null);
            var w = Application.Current.Windows.OfType<AiModelSettingsWindow>().Single();
            w.Width = 900; w.Height = 1000; return w;
        }
        public static void Click(Window window, string name)
        {
            var button = (Button)window.FindName(name);
            if (button.Command is { } command) { if (command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter); }
            else button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
        public static void Input(Window w, string name, string text)
        {
            var box = (TextBox)w.FindName(name); box.Text = text;
            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }
        public static Border Card(Window w, string name)
        {
            DependencyObject? current = (FrameworkElement)w.FindName(name);
            while (current is not null && current is not Border) current = VisualTreeHelper.GetParent(current);
            return (Border)(current ?? throw new InvalidOperationException("Settings card not found: " + name));
        }
        public static async Task ScrollCard(AiModelSettingsWindow w, string name)
        {
            var scroll = (ScrollViewer)w.FindName("SettingsScroll");
            var card = Card(w, name);
            double y = card.TransformToAncestor((FrameworkElement)scroll.Content).Transform(new Point()).Y;
            scroll.ScrollToVerticalOffset(Math.Max(0, y - 8)); await Settle(w);
        }
        public static async Task CardShot(AiModelSettingsWindow w, string name, string id, string topic, string state)
        {
            await ScrollCard(w, name);
            Shot(w, id, topic, state, [], Bounds(w, Card(w, name)));
        }
        public async Task Change(Action<AiModelSettingsViewModel, AiModelSettingsWindow> change)
        {
            Ai.IsOpen = false;
            var w = Settings(); await Settle(w);
            var draft = (AiModelSettingsViewModel)w.DataContext;
            change(draft, w);
            Click(w, "SaveModelButton");
            Check(!w.IsVisible, "Valid model-settings changes save through the real window handler.");
            await AiUntil(() => !Engine.IsBusy && !Ai.IsBusy && !Engine.IsLoaded, "Settings reload boundary");
            Ai.IsOpen = true; await Settle(Main);
        }
        public async Task<string> Ready()
        {
            Ai.Prompt = Local("Ответь одним словом: готово.", "Відповідай одним словом: готово.", "Antworte mit einem Wort: bereit.", "Reply with one word: ready.");
            await Ai.Send.ExecuteAsync(); await Settle(Main);
            Check(Engine.IsLoaded && Ai.Messages.Last().HasContent, "Real generation succeeds with the selected saved runtime parameters.");
            return Ai.Messages.Last().Content;
        }
    }

    private static async Task ValidateStage93Settings(IsolatedServices isolated, Stage93Host h, IAiRuntimeStatusSource runtime)
    {
        var ai = h.Ai; var store = h.Store; var engine = h.Engine; var main = h.Main;
        string before = File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json"));
        var w = h.Settings(); await Settle(w); var draft = (AiModelSettingsViewModel)w.DataContext;
        Stage93Host.Input(w, "ContextBox", "256"); Stage93Host.Click(w, "SaveModelButton");
        Check(w.IsVisible && draft.Status == LocalizationManager.GetString("AI.InvalidContextLength") && File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json")) == before, "Out-of-range context is rejected without replacing saved settings."); w.Close();
        w = h.Settings(); await Settle(w); draft = (AiModelSettingsViewModel)w.DataContext;
        draft.ReuseContext = true; draft.PreloadOnPanelOpen = true; draft.ContextLength = 2048; w.Close();
        Check(File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json")) == before, "Closing the settings draft discards context and session-option edits.");
        ai.IsOpen = false; w = h.Settings(); await Settle(w); draft = (AiModelSettingsViewModel)w.DataContext;
        Stage93Host.Input(w, "ContextBox", "1024"); Stage93Host.Input(w, "TokensBox", "2048");
        ((CheckBox)w.FindName("ReuseContextCheckBox")).IsChecked = true;
        ((CheckBox)w.FindName("PreloadOnPanelOpenCheckBox")).IsChecked = true;
        await Stage93Host.CardShot(w, "ContextBox", "S38-e", "ai.context", "requested context and reply ceiling, pending Save");
        await Stage93Host.CardShot(w, "PerformanceModeBox", "S38-f", "ai.context", "opt-in reuse and preload are independent saved model options");
        Stage93Host.Click(w, "SaveModelButton"); await Settle(main);
        Check(!w.IsVisible && store.Current.SelectedModel.ContextLength == 1024 && store.Current.SelectedModel.MaxTokens == 2048 && store.Current.SelectedModel.ReuseContext && store.Current.SelectedModel.PreloadOnPanelOpen, "Save commits reply ceiling above context and both opt-in session flags without clamping.");
        ai.IsOpen = true; await AiUntil(() => engine.IsLoaded && !ai.IsBusy, "Real panel preload", 240); await Settle(main);
        Check(ai.Messages.Count == 0 && ai.RemainingTokens == ai.ContextLength, "Opening the panel preloads real weights without inventing a chat and shows the whole free context.");
        h.PanelShot("S38-g", "ai.context", "actual preload, no conversation yet");
        AiTokenUsage? latest = null; var usages = new List<AiTokenUsage>();
        void Usage(AiTokenUsage value) { Volatile.Write(ref latest, value); lock (usages) usages.Add(value); }
        ((IAiTokenUsageSource)engine).TokenUsageChanged += Usage;
        ai.Prompt = Local("Напиши очень длинный список из 1000 разных советов вымышленной библиотеке. Подробно опиши каждый пункт.", "Напиши дуже довгий список із 1000 різних порад вигаданій бібліотеці. Докладно опиши кожен пункт.", "Schreibe eine sehr lange Liste mit 1000 verschiedenen ausführlichen Tipps für eine fiktive Bibliothek.", "Write a very long list of 1000 different detailed suggestions for a fictional library.");
        Task sending = ai.Send.ExecuteAsync();
        await AiUntil(() => ai.IsBusy && ai.Messages.Last().Content.Length > 40 && Volatile.Read(ref latest) is not null, "Real token usage while streaming", 240);
        // Appearance changes during a real request must not cancel it.
        ai.OpenAppearance.Execute(null); var appearance = Application.Current.Windows.OfType<AiChatAppearanceWindow>().Single(); await Settle(appearance);
        ((ComboBox)appearance.FindName("FontSizeBox")).SelectedItem = 16d; Stage93Host.Click(appearance, "SaveAppearanceButton");
        Check(ai.IsBusy && engine.IsLoaded && store.Current.ChatAppearance.FontSize == 16, "Saving chat typography during actual generation does not stop or unload the model.");
        await AiUntil(() => ai.IsBusy && ai.ContextSummary == LocalizationManager.Format("AI.ContextUsage", ai.RemainingTokens, ai.ContextLength, ai.ReplyTokenLimit), "Measured context label");
        Check(ai.ContextLength == runtime.RuntimeOptions!.ContextLength && ai.ReplyTokenLimit <= ai.RemainingTokens && ai.ReplyTokenLimit <= store.Current.SelectedModel.MaxTokens, "Measured context and remaining reply allowance follow actual runtime usage, capped by free context.");
        h.PanelShot("S40-e", "ai.context", "real measured input/output tokens during generation");
        ai.Stop.Execute(null); await sending; ((IAiTokenUsageSource)engine).TokenUsageChanged -= Usage;
        await ai.ClearChat.ExecuteAsync(); await Settle(main);
        Check(engine.IsLoaded && ai.RemainingTokens == ai.ContextLength, "New chat resets budget and native computation context while retaining real weights.");
        ai.Prompt = string.Join('\n', Enumerable.Range(0, 2000).Select(i => $"fictional-route-{i:D4} orbit-{7000 + i:D4}")); await Settle(main);
        Check(ai.IsContextLow && ai.RemainingTokens == 0 && ai.ContextSummary.StartsWith("≈", StringComparison.Ordinal), "Large synthetic draft produces an estimated full-context warning without loading another model.");
        h.PanelShot("S40-d", "ai.context", "estimated full draft before sending, no fabricated tokenizer measurement");
        await ai.Send.ExecuteAsync(); await Settle(main);
        Check(ai.Status == LocalizationManager.GetString("AI.ContextFull") && !engine.IsLoaded, "The actual tokenizer rejects an oversized request with ContextFull and releases the failed worker.");
        h.PanelShot("S40-f", "ai.context", "actual context-full rejection"); await ai.ClearChat.ExecuteAsync();
        ai.Prompt = Local("Выведи без пояснений: заголовок Markdown «План», затем **Маршрут**, затем блок кода text с одной строкой ORBIT-731.", "Виведи без пояснень: заголовок Markdown «План», потім **Маршрут**, потім блок коду text з одним рядком ORBIT-731.", "Gib ohne Erklärung eine Markdown-Überschrift Plan, dann **Route** und einen text-Codeblock mit der Zeile ORBIT-731 aus.", "Output only a Markdown heading Plan, then **Route**, then a text code block containing ORBIT-731.");
        await ai.Send.ExecuteAsync(); await Settle(main); var message = ai.Messages.Last(); string answer = message.Content;
        Check(message.HasContent && answer.Contains("ORBIT-731") && answer.Contains("```"), "Actual model reply provides source Markdown and a code block for appearance checks.");
        ai.Prompt = Local("Сохранённый черновик.", "Збережена чернетка.", "Erhaltener Entwurf.", "Preserved draft.");
        ai.Attachments.Add(AiAttachmentService.Document("Draft.txt", "Fictional attachment."));
        string savedPrompt = ai.Prompt; var savedAttachment = ai.Attachments.Single();
        before = File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json"));
        ai.OpenAppearance.Execute(null); appearance = Application.Current.Windows.OfType<AiChatAppearanceWindow>().Single(); await Settle(appearance);
        var avm = (AiChatAppearanceViewModel)appearance.DataContext; avm.FontSize = 24; appearance.Close();
        Check(File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json")) == before, "Closing Chat appearance discards unsaved typography.");
        ai.OpenAppearance.Execute(null); appearance = Application.Current.Windows.OfType<AiChatAppearanceWindow>().Single(); appearance.Width = 600; appearance.Height = 1000; await Settle(appearance);
        avm = (AiChatAppearanceViewModel)appearance.DataContext;
        Check(avm.Fonts.Contains("Consolas"), "Installed Consolas font is available in the real selector.");
        ((ComboBox)appearance.FindName("FontBox")).SelectedItem = "Consolas";
        ((ComboBox)appearance.FindName("CodeFontBox")).SelectedItem = "Consolas";
        ((ComboBox)appearance.FindName("FontSizeBox")).SelectedItem = 16d;
        ((TextBox)appearance.FindName("UserColorBox")).Text = "#003399";
        ((TextBox)appearance.FindName("AssistantColorBox")).Text = "#31522F";
        ((TextBox)appearance.FindName("CodeColorBox")).Text = "#6B228B";
        avm.ShowMessageBorders = true; await Settle(appearance);
        Shot(appearance, "S45-a", "ai.appearance", "font, size, question/answer/code colors and borders", []);
        ((TextBox)appearance.FindName("CodeColorBox")).Text = "bad-color"; Stage93Host.Click(appearance, "SaveAppearanceButton");
        Check(appearance.IsVisible && avm.Status == LocalizationManager.GetString("AI.AppearanceInvalid") && File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json")) == before, "Invalid appearance color is rejected without saving or clearing the chat.");
        ((TextBox)appearance.FindName("CodeColorBox")).Text = "#6B228B"; Stage93Host.Click(appearance, "SaveAppearanceButton"); await Settle(main);
        var shown = Visuals<RichTextBox>(main).Single(box => box.Name == "MessageContent" && ReferenceEquals(box.DataContext, message));
        Check(shown.Document.FontFamily.Source == "Consolas" && shown.Document.FontSize == 16 && shown.Document.Foreground.ToString() == "#FF31522F" &&
            Descendants(shown.Document).OfType<Run>().Any(run => run.Text.Contains("ORBIT-731") && run.Foreground.ToString() == "#FF6B228B"), "Saved typography and separate code color reach the already rendered real reply.");
        Check(engine.IsLoaded && message.Content == answer && ai.Prompt == savedPrompt && ReferenceEquals(ai.Attachments.Single(), savedAttachment), "Appearance Save preserves actual chat, draft, attachments and loaded weights.");
        h.PanelShot("S45-b", "ai.appearance", "same generated Markdown reply with saved typography and borders");
        ai.OpenAppearance.Execute(null); appearance = Application.Current.Windows.OfType<AiChatAppearanceWindow>().Single(); await Settle(appearance);
        ((CheckBox)appearance.FindName("RenderMarkdownCheckBox")).IsChecked = false; Stage93Host.Click(appearance, "SaveAppearanceButton"); await Settle(main);
        Check(Text(shown.Document).Contains("```") && message.Content == answer && engine.IsLoaded && ai.Prompt == savedPrompt, "Disabling Render Markdown shows original source without resetting the conversation.");
        ai.Copy.Execute(null); Check(isolated.TextClipboard.Text == answer, "Copy still returns the same source after switching the visual rendering mode.");
        h.PanelShot("S45-c", "ai.appearance", "raw Markdown display of the same reply");
        ai.OpenAppearance.Execute(null); appearance = Application.Current.Windows.OfType<AiChatAppearanceWindow>().Single(); appearance.Width = 600; appearance.Height = 1000; await Settle(appearance);
        avm = (AiChatAppearanceViewModel)appearance.DataContext; avm.Reset.Execute(null); await Settle(appearance);
        Check(avm.FontSize == 14 && avm.RenderMarkdown && !avm.ShowMessageBorders && avm.CodeColor == "", "Reset changes the appearance draft to defaults before saving.");
        Shot(appearance, "S45-d", "ai.appearance", "reset appearance draft, Save still required", []); Stage93Host.Click(appearance, "SaveAppearanceButton"); await Settle(main);
        Check(store.Current.ChatAppearance == new AiChatAppearance() && engine.IsLoaded && ai.Prompt == savedPrompt && ReferenceEquals(ai.Attachments.Single(), savedAttachment), "Reset then Save restores defaults and retains the current work.");
        ai.RemoveAttachment.Execute(savedAttachment); ai.Prompt = "";

        w = h.Settings(); await Settle(w); await Stage93Host.CardShot(w, "MemoryModeBox", "S44-a", "ai.memory", "automatic placement and optional context reduction"); w.Close();
        await h.Change((d, window) => { d.PreloadOnPanelOpen = false; d.MemoryMode = AiMemoryMode.Manual; Stage93Host.Input(window, "GpuBox", "2"); Stage93Host.Input(window, "TokensBox", "32"); });
        w = h.Settings(); await Settle(w); await Stage93Host.CardShot(w, "MemoryModeBox", "S44-b", "ai.memory", "manual GPU layer count is saved for this model"); w.Close();
        await h.Ready(); var manualOptions = runtime.RuntimeOptions;
        Check(manualOptions is { GpuLayers: 2, CpuBackend: false }, "Real worker honors two manually requested GPU layers.");
        await h.Change((d, _) => d.MemoryMode = AiMemoryMode.CpuOnly); await h.Ready(); var cpuOptions = runtime.RuntimeOptions;
        Check(cpuOptions is { GpuLayers: 0, CpuBackend: true }, "CPU-only mode actually runs with zero offloaded layers.");
        Named<Expander>(main, "RuntimeDetailsExpander").IsExpanded = true; await Settle(main);
        h.PanelShot("S44-c", "ai.memory", "actual CPU-only runtime profile, not a substituted GPU count");
        await h.Change((d, window) => { d.MemoryMode = AiMemoryMode.Automatic; d.UseReasoning = true; d.RepetitionProtection = false; Stage93Host.Input(window, "ContextBox", "2048"); Stage93Host.Input(window, "TokensBox", "512"); });
        Check(!store.Current.SelectedModel.RepetitionProtection && store.Current.SelectedModel.UseReasoning, "Reasoning and repeat-protection options save independently per model.");
        await h.Change((d, _) => d.RepetitionProtection = true);
        w = h.Settings(); await Settle(w); await Stage93Host.CardShot(w, "ReasoningCheckBox", "S44-d", "ai.memory", "reasoning and repeat protection with the model-support warning"); w.Close();
        var reasoningMessage = await CaptureStage93Reasoning(isolated, h);
        WriteJson(Path.GetRelativePath(repo, Path.Combine(isolated.Root, "stage9-settings.json")), new { culture, model = Path.GetFileName(store.Current.SelectedModelPath), requestedContext = 1024, requestedReplyCeiling = 2048, measuredUsage = usages, manualOptions, cpuOptions, automaticOptions = runtime.RuntimeOptions, nativeReasoningProvided = reasoningMessage.HasReasoning, reasoning = reasoningMessage.Reasoning, reasoningAnswer = reasoningMessage.Content, appearanceAnswer = answer, clipboard = "Memory sink only", forceSystemMemoryPressure = false });
    }

    private static async Task<AiChatMessage> CaptureStage93Reasoning(IsolatedServices isolated, Stage93Host h)
    {
        var ai = h.Ai; var main = h.Main;
        Named<Expander>(main, "RuntimeDetailsExpander").IsExpanded = false;
        ai.Prompt = Local("Вычисли 17 + 25, затем напиши короткий ответ с числом.", "Обчисли 17 + 25, потім напиши коротку відповідь із числом.", "Berechne 17 + 25 und gib dann eine kurze Antwort mit der Zahl.", "Compute 17 + 25, then give a short answer with the number.");
        await ai.Send.ExecuteAsync(); await Settle(main); var reasoningMessage = ai.Messages.Last();
        Check(reasoningMessage.Content.Contains("42"), "Real reply with reasoning enabled reaches the expected arithmetic answer.");
        if (reasoningMessage.HasReasoning)
        {
            Visuals<Expander>(main).Single(e => e.Name == "ReasoningExpander" && ReferenceEquals(e.DataContext, reasoningMessage)).IsExpanded = true;
            await Settle(main);
            ai.Copy.Execute(null);
            Check(isolated.TextClipboard.Text == reasoningMessage.Content && !reasoningMessage.Content.Contains("<think>"), "Actual marked reasoning is displayed separately and excluded from Copy.");
        }
        Named<ScrollViewer>(main, "ConversationScroll").ScrollToTop(); await Settle(main);
        h.PanelShot("S44-e", "ai.memory", "actual model reply with reasoning enabled; separate analysis shown only when provided");
        return reasoningMessage;
    }

    private static async Task ValidateStage93Performance(IsolatedServices isolated, Stage93Host h, IAiRuntimeStatusSource runtime, bool onAc)
    {
        var ai = h.Ai; var engine = h.Engine; var store = h.Store; var main = h.Main;
        string first = await h.Ready(); var balanced = runtime.RuntimeOptions;
        Check(balanced is { PowerSaving: false, PerformanceBoost: false }, "Balanced real generation uses normal scheduling without performance boost.");
        ai.Prompt = Local("Черновик.", "Чернетка.", "Entwurf.", "Draft."); string draftText = ai.Prompt;
        int messageCount = ai.Messages.Count;
        await h.Change((d, _) => d.PerformanceMode = AiPerformanceMode.PowerSaving);
        Check(ai.Messages.Count == messageCount && ai.Prompt == draftText && !engine.IsLoaded, "Saving Power saving preserves conversation/draft and releases the old worker for reload.");
        await h.Ready(); var saving = runtime.RuntimeOptions;
        Check(saving is { PowerSaving: true, PerformanceBoost: false } && saving.Threads == Math.Max(1, Environment.ProcessorCount / 4), "Real Power saving worker uses the reduced thread profile.");
        Named<Expander>(main, "RuntimeDetailsExpander").IsExpanded = true; await Settle(main); h.PanelShot("S44-g", "ai.performance", "actual power-saving threads and runtime");
        var w = h.Settings(); await Settle(w); var draft = (AiModelSettingsViewModel)w.DataContext;
        ((ComboBox)w.FindName("PerformanceModeBox")).SelectedValue = AiPerformanceMode.Optimal;
        ((CheckBox)w.FindName("BoostOnlyOnAcCheckBox")).IsChecked = true;
        await Stage93Host.ScrollCard(w, "PerformanceModeBox");
        Check(store.Current.SelectedModel.EffectivePerformanceMode == AiPerformanceMode.PowerSaving && draft.IsOptimalPerformanceMode, "Choosing Optimal in a settings draft does not save the mode implicitly.");
        int notices = 0;
        void Trace(object? _, EventArgs __)
        {
            var current = runtime.Notices;
            if (notices > current.Count) notices = 0;
            foreach (var notice in current.Skip(notices).Where(n => n.ResourceKey.StartsWith("AI.Performance", StringComparison.Ordinal)))
                Console.WriteLine($"BENCH {culture} {notice.ResourceKey} {string.Join(',', notice.Arguments)}");
            notices = current.Count;
        }
        runtime.RuntimeChanged += Trace;
        try
        {
            Stage93Host.Click(w, "PerformanceTestButton");
            await AiUntil(() => !draft.IsBusy || runtime.Notices.Any(n => n.ResourceKey == "AI.PerformanceTrial"), "Actual performance trial starts", 240);
            Check(onAc && draft.IsBusy, "Plugged-in hardware reaches a real benchmark trial with the AC-only option retained.");
            await Settle(w); Shot(w, "S44-h", "ai.performance", "real benchmark in progress, Stop available", []);
            draft.StopTest.Execute(null); await AiUntil(() => !draft.IsBusy && !engine.IsBusy, "Benchmark Stop", 30); await Settle(w);
            Check(draft.Status == LocalizationManager.GetString("AI.Stopped") && !engine.IsLoaded, "Stop cancels the native benchmark, releases its worker and reports cancellation.");
            Shot(w, "S44-i", "ai.performance", "actual stopped benchmark; retry remains available", []);
            Stage93Host.Click(w, "PerformanceTestButton");
            await AiUntil(() => !draft.IsBusy, "Complete native benchmark", 450); await Settle(w);
            var result = ((IAiPerformanceService)engine).GetLastPerformance(store.Current.SelectedModel with { PerformanceMode = AiPerformanceMode.Optimal });
            Check(draft.Status == LocalizationManager.GetString("AI.PerformanceDone") && result is not null && result.Baseline.PromptTokensPerSecond > 0 && result.Selected.GenerationTokensPerSecond > 0, "Full native benchmark records measured prompt/generation speeds and a selected result.");
            Check(!result!.BoostSelected || result.Speedup >= 1.05, "The measured boost is chosen only at the five-percent threshold.");
            Check(store.Current.SelectedModel.EffectivePerformanceMode == AiPerformanceMode.PowerSaving && ai.Messages.Count >= messageCount, "Benchmarking itself preserves saved mode and conversation.");
            await Stage93Host.CardShot(w, "PerformanceModeBox", "S44-f", "ai.performance", "actual measured baseline and selected result, pending Save");
            Stage93Host.Click(w, "SaveModelButton"); await Settle(main);
            await AiUntil(() => !engine.IsLoaded && !engine.IsBusy, "Optimal save reload boundary");
            await h.Ready(); var optimal = runtime.RuntimeOptions;
            Check(optimal is not null && optimal.PerformanceBoost == result.BoostSelected && !optimal.PowerSaving, "Saved Optimal mode applies the measured result in an actual subsequent request.");
            w = h.Settings(); await Settle(w); draft = (AiModelSettingsViewModel)w.DataContext;
            Stage93Host.Click(w, "RetuneModelButton"); await AiUntil(() => !draft.IsBusy, "Retune model", 240);
            Check(((IAiPerformanceService)engine).GetLastPerformance(store.Current.SelectedModel) is null && engine.IsLoaded, "Retune clears this model's old measured/runtime profiles and completes a real fresh model test.");
            w.Close();
            WriteJson(Path.GetRelativePath(repo, Path.Combine(isolated.Root, "stage9-performance.json")), new { culture, onAc, logicalProcessors = Environment.ProcessorCount, model = Path.GetFileName(store.Current.SelectedModelPath), balanced, saving, optimal, measured = result, stop = "Native benchmark cancelled at a real trial", retune = "Fresh model test after profile removal", otherProcessesChanged = false });
        }
        finally { runtime.RuntimeChanged -= Trace; }
    }
}
