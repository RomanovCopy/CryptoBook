using Autofac;
using CryptoBook.AI;
using CryptoBook.DTO;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

internal static partial class Program
{
    private static readonly string[] Stage10Topics = ["start.install", "settings.appearance", "settings.language", "settings.about", "settings.updates", "settings.data", "settings.hotkeys", "settings.troubleshoot", "help.use"];

    private static async Task ValidateStage10(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        main.Width = 1440; main.Height = 960; main.WindowState = WindowState.Normal;
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        var service = c.Resolve<IHelpWindowService>();
        if (verify)
        {
            service.Open(); await Settle(main);
            var window = Application.Current.Windows.OfType<HelpWindow>().Single();
            var help = (HelpWindowViewModel)window.DataContext;
            var content = c.Resolve<HelpContentService>();
            foreach (string id in Stage10Topics)
            {
                Check(help.OpenTopic(id), $"Embedded stage10 topic opens: {id}."); await Settle(window);
                var article = content.Find(culture, id)!;
                Check(article.Images.All(path => content.LoadImage(culture, article, path) is not null), $"All embedded stage10 images load: {id}.");
                if (id is "settings.hotkeys" or "help.use")
                    SaveWindow(window, $"docs/help/screenshots/validation/stage10/article-{id}-{culture}.png");
            }
            Check(content.GetLocale(culture).Topics.Count == 76, "All 76 topics are available in this culture.");
            window.Close(); return;
        }
        await Settle(main);
        Shot(main, "S01-a", "start.install", "application start screen; not an installer wizard", []);

        // Both document types remain dirty while settings/help are used.
        string textPath = Path.Combine(isolated.DemoDirectory, "Settings-example.txt");
        string mdPath = Path.Combine(isolated.DemoDirectory, "Settings-example.md");
        File.WriteAllText(textPath, Local("Учебный план библиотеки.", "Навчальний план бібліотеки.", "Übungsplan der Bibliothek.", "Practice library plan."));
        File.WriteAllText(mdPath, "# ORBIT-731\n\nFictional library schedule.");
        Check((await c.Resolve<IWorkspaceFileOpenService>().OpenAsync(mdPath)).Success, "Disposable Markdown opens.");
        var md = c.Resolve<IMarkdownDocumentState>(); md.Text += "\nUnsaved Markdown marker.";
        Check((await c.Resolve<IWorkspaceFileOpenService>().OpenAsync(textPath)).Success, "Disposable ordinary document opens.");
        demonstrationFile = textPath;
        var editor = c.Resolve<IRichTextBoxService>(); var session = c.Resolve<IDocumentSession>();
        // WPF registers the standard gestures on RichTextBox's class, not on command.InputGestures.
        // The primary-source mapping is recorded in the stage report; exercise the real command route here.
        var textEditor = (System.Windows.Controls.RichTextBox)editor;
        await Settle(main); textEditor.Focus();
        textEditor.SelectAll();
        foreach (var (command, key, property) in new[]
        {
            (EditingCommands.ToggleBold, Key.B, TextElement.FontWeightProperty),
            (EditingCommands.ToggleItalic, Key.I, TextElement.FontStyleProperty),
            (EditingCommands.ToggleUnderline, Key.U, Inline.TextDecorationsProperty)
        })
        {
            Check(command.CanExecute(null, textEditor), $"The formatting command associated with Ctrl+{key} routes to the active rich-text editor.");
            object before = textEditor.Selection.GetPropertyValue(property);
            command.Execute(null, textEditor);
            Check(!Equals(textEditor.Selection.GetPropertyValue(property), before), $"The live {command.Name} command changes the selected ordinary text.");
            textEditor.Undo();
        }
        textEditor.Selection.Select(textEditor.Document.ContentEnd, textEditor.Document.ContentEnd);
        editor.Document.Blocks.Add(new Paragraph(new Run("Unsaved ordinary marker."))); session.MarkDirty();
        string ordinary = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
        var workspace = (IWorkspaceDocumentSession)session;
        var inactive = workspace.CaptureInactiveDocument();
        Check(inactive is { IsMarkdown: true } && System.Text.Encoding.UTF8.GetString(inactive.Content).Contains("Unsaved Markdown marker."), "Dirty inactive Markdown is retained.");
        var ai = c.Resolve<AiAssistantViewModel>();
        var message = new AiChatMessage("user", "Fictional settings check."); ai.Messages.Add(message); ai.Prompt = "Unsent ORBIT-731";

        c.Resolve<ISettingsWindowService>().Open(); await Settle(main);
        var settings = Application.Current.Windows.OfType<SettingsWindow>().Single();
        settings.Width = 1060; settings.Height = 1000;
        var vm = (ISettingsViewModel)settings.DataContext;
        vm.SelectedSectionIndex = 0; await Settle(settings);
        var settingsNavigation = Visuals<ListBox>(settings).Single(b => b.Items.Count == 7);
        Rect navigationBounds = Bounds(settings, settingsNavigation);
        var labels = Visuals<TextBlock>(settingsNavigation).Where(t => t.FontFamily.Source != "Segoe MDL2 Assets" && t.Text != "AI").ToArray();
        Check(labels.Length == 7 && labels.All(t => { Rect b = Bounds(settings, t); return b.Right <= navigationBounds.Right + 1 && b.Left >= navigationBounds.Left && t.DesiredSize.Width <= t.ActualWidth + 1; }), "All seven localized settings navigation labels fit the default pane without horizontal clipping.");
        foreach (var option in vm.Themes)
        {
            vm.SelectedTheme = option; await Settle(settings);
            Check(c.Resolve<IThemeManager>().CurrentTheme == option.Theme && new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text == ordinary, $"Theme {option.Theme} applies immediately without changing the document.");
        }
        vm.SelectedTheme = vm.Themes.Single(t => t.Theme == ApplicationTheme.Light); await Settle(settings);
        Shot(settings, "S46-a", "settings.appearance", "four real theme choices", []);
        var screen = Named<ComboBox>(settings, "StartupScreenSelector");
        screen.SelectedItem = vm.StartupScreens.Single(s => s.Screen == StartupScreen.Editor);
        var template = Named<ComboBox>(settings, "DefaultDocumentTemplateSelector");
        var chosen = vm.DocumentTemplates.First(t => t.Id != vm.SelectedDefaultDocumentTemplate.Id);
        template.SelectedItem = chosen;
        Check(c.Resolve<IStartupScreenPreferenceStore>().Load() == StartupScreen.Editor && c.Resolve<IDocumentTemplatePreferenceStore>().Load() == chosen.Id, "Startup and default-template selectors immediately save through production preference stores.");
        Check(session.IsDirty && session.FilePath == textPath, "Changing startup screen does not navigate or close the current document.");
        screen.BringIntoView(); await Settle(settings);
        Rect startupBounds = Bounds(settings, screen);
        Shot(settings, "S46-b", "settings.appearance", "startup and default-template selectors; no Save button", [], new Rect(startupBounds.X - 8, startupBounds.Y - 34, settings.ActualWidth - startupBounds.X - 20, 290));

        service.Open(); await Settle(main);
        var helpWindow = Application.Current.Windows.OfType<HelpWindow>().Single();
        var helpVm = (HelpWindowViewModel)helpWindow.DataContext;
        helpVm.OpenTopic("doc.save"); helpVm.Query = "09:00"; helpVm.RunSearch();
        vm.SelectedSectionIndex = 3; await Settle(settings);
        var languageBox = Visuals<ComboBox>(settings).Single(b => b.IsVisible && b.ItemsSource == vm.Languages);
        string otherCulture = culture == "en-US" ? "de-DE" : "en-US";
        languageBox.SelectedItem = vm.Languages.Single(l => l.CultureName == otherCulture); await Settle(settings);
        Check(LocalizationManager.CurrentCultureName == otherCulture && helpVm.Culture == otherCulture && helpVm.CurrentTopicId == "doc.save" && helpVm.Query.Length == 0 && helpVm.Results.Count == 0, "Changing the actual language selector updates open help, retains topic and clears old-language search.");
        languageBox.SelectedItem = vm.Languages.Single(l => l.CultureName == culture); await Settle(settings);
        Check(CryptoBook.Properties.Settings.Default.CultureInfo == culture && helpVm.Culture == culture && settings.Title == LocalizationManager.GetString("Settings.WindowTitle"), "Language returns to the requested culture and saves immediately.");
        Shot(settings, "S46-c", "settings.language", "four supported languages, immediate application", []);
        helpWindow.Close();

        vm.SelectedSectionIndex = 1; await Settle(settings);
        vm.ChooseWorkspace.Execute(null);
        await AiUntil(() => vm.WorkspaceDirectory == isolated.DemoDirectory, "Disposable workspace selection", 10);
        Check(vm.WorkspaceDirectory == isolated.DemoDirectory, "Workspace selection uses only the isolated demo folder.");
        Shot(settings, "S46-d", "settings.data", "workspace is a document folder, separate from user settings", []);

        vm.SelectedSectionIndex = 4; await Settle(settings);
        Check(vm.ApplicationVersion == "1.1.5.2", "About shows the real assembly release version.");
        Shot(settings, "S47-a", "settings.about", "real product information and version", []);
        vm.SelectedSectionIndex = 5; await Settle(settings);
        Shot(settings, "S47-b", "settings.about", "real developer information; not an external navigation command", []);
        vm.SelectedSectionIndex = 4; await Settle(settings);
        var update = vm.UpdateNotification;
        var button = Buttons(settings).Single(b => b.Command == vm.CheckForUpdates);
        button.BringIntoView(); await Settle(settings);
        isolated.Releases.Release = null;
        isolated.Releases.Gate = new TaskCompletionSource();
        Task checking = ((AsyncRelayCommand)vm.CheckForUpdates).ExecuteAsync(); await Settle(settings);
        Check(vm.UpdateCheckStatus == LocalizationManager.GetString("Update.Checking") && !vm.CheckForUpdates.CanExecute(null), "Manual check displays busy status and disables concurrent checks.");
        Stage10UpdateShot(settings, button, "S47-c", "settings.updates", "checking via production command; isolated release source");
        isolated.Releases.Gate.SetResult(); await checking; isolated.Releases.Gate = null;
        Check(vm.UpdateCheckStatus == LocalizationManager.GetString("Update.UpToDate") && !update.IsVisible, "No newer release hides the banner and reports current version.");
        Check(SemanticVersion.TryParse("99.0.0", out var demoVersion), "Fictional update version parses.");
        isolated.Releases.Release = new ApplicationRelease(demoVersion!, "Help demonstration", "Fictional release; never downloaded.", new Uri("https://github.com/RomanovCopy/CryptoBook/releases/tag/v99.0.0"), null);
        await ((AsyncRelayCommand)vm.CheckForUpdates).ExecuteAsync(); await Settle(settings);
        Check(update.IsVisible && update.Title.Contains("99.0.0") && update.OpenRelease.CanExecute(null), "A fictional newer release appears through production version comparison and notification.");
        button.BringIntoView(); await Settle(settings);
        Stage10UpdateShot(settings, button, "S47-d", "settings.updates", "fictional version 99.0.0, real notification and action labels; installation not executed");
        update.RemindLater.Execute(null);
        Check(!update.IsVisible, "Later dismisses the current notification.");
        await ((AsyncRelayCommand)vm.CheckForUpdates).ExecuteAsync();
        await ((AsyncRelayCommand)update.SkipVersion).ExecuteAsync();
        var state = await c.Resolve<IUpdateCheckStateStore>().LoadAsync();
        Check(!update.IsVisible && state.SkippedVersion == "99.0.0", "Skip persists only the demonstrated version in the isolated update store.");
        int calls = isolated.Releases.Calls;
        await c.Resolve<IApplicationUpdateCoordinator>().CheckAsync();
        Check(isolated.Releases.Calls == calls, "Automatic check respects its interval after a manual check.");
        await ((AsyncRelayCommand)vm.CheckForUpdates).ExecuteAsync();
        Check(update.IsVisible, "Manual check bypasses interval and skipped-version suppression.");
        isolated.Releases.Failure = new HttpRequestException("Fictional offline state.");
        await ((AsyncRelayCommand)vm.CheckForUpdates).ExecuteAsync(); await Settle(settings);
        Check(vm.UpdateCheckStatus == LocalizationManager.GetString("Update.CheckFailed") && !update.IsVisible, "Network failure reports a localized retry action and hides stale release information.");
        Stage10UpdateShot(settings, button, "S47-e", "settings.troubleshoot", "real localized failure state from an injected transport failure");
        isolated.Releases.Failure = null;
        isolated.Releases.Gate = new TaskCompletionSource();
        using (var stop = new CancellationTokenSource())
        {
            Task pending = update.CheckNowAsync(stop.Token); stop.Cancel(); await pending;
        }
        isolated.Releases.Gate = null;
        Check(vm.UpdateCheckStatus == LocalizationManager.GetString("Update.CheckCanceled") && !update.IsVisible, "Canceled manual check has a distinct state without starting an installer.");
        vm.Close.Execute(null); await Settle(main);
        c.Resolve<ISettingsWindowService>().Open(); await Settle(main);
        settings = Application.Current.Windows.OfType<SettingsWindow>().Single(); vm = (ISettingsViewModel)settings.DataContext;
        Check(vm.SelectedStartupScreen.Screen == StartupScreen.Editor && vm.SelectedDefaultDocumentTemplate.Id == chosen.Id && vm.SelectedCultureName == culture, "Reopening settings restores the chosen startup/template/language from isolated preferences.");
        vm.Close.Execute(null); await Settle(main);

        // This invokes the main window's actual F1 behavior, not only its view model.
        var visibleEditor = Visuals<System.Windows.Controls.RichTextBox>(main).Single(r => r.IsVisible);
        visibleEditor.Focus(); visibleEditor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(main), Environment.TickCount, Key.F1) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        await Settle(main);
        helpWindow = Application.Current.Windows.OfType<HelpWindow>().Single(); helpVm = (HelpWindowViewModel)helpWindow.DataContext;
        helpWindow.Width = 1280; helpWindow.Height = 920;
        Check(helpWindow.Owner == main && main.IsEnabled, "Routed F1 opens modeless help owned by the active main window.");
        service.Open(); Check(Application.Current.Windows.OfType<HelpWindow>().Count() == 1, "Menu-equivalent reopening activates one existing help instance.");
        helpVm.OpenTopic("start.interface"); await Settle(helpWindow);
        Shot(helpWindow, "S48-a", "help.use", "contents and actual article", []);
        var search = Named<TextBox>(helpWindow, "HelpSearch");
        search.Text = Local("сохранить", "зберегти", "speichern", "save");
        search.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); helpVm.RunSearch(); await Settle(helpWindow);
        Check(helpVm.Results.Count > 0 && helpVm.Results.All(r => c.Resolve<HelpContentService>().Find(culture, r.Id) is not null), "Search returns available articles from only the current language.");
        Shot(helpWindow, "S48-b", "help.use", "localized search results", []);
        search.Focus(); RoutedKey(helpWindow, Key.Enter); await Settle(helpWindow);
        Check(helpVm.CurrentTopicId == helpVm.SelectedResult!.Id, "Enter in the real search field opens its selected result.");
        search.Text = "no-match-ORBIT-731-999"; helpVm.RunSearch();
        Check(helpVm.HasNoResults, "An unmatched query has a visible empty-results state.");
        search.Focus(); RoutedKey(helpWindow, Key.Escape); await Settle(helpWindow);
        Check(helpVm.Query.Length == 0 && !helpVm.IsSearching, "Escape in search clears the query and returns contents.");
        helpVm.OpenTopic("doc.create"); await Settle(helpWindow);
        var imageButton = Visuals<Button>(helpWindow).First(b => b.IsVisible && b.Command == helpVm.ShowImage);
        imageButton.BringIntoView(); await Settle(helpWindow); imageButton.Focus();
        double offset = helpVm.ScrollOffset;
        imageButton.Command!.Execute(imageButton.CommandParameter); await Settle(helpWindow);
        Check(helpVm.IsImageOpen && Named<Button>(helpWindow, "CloseImageButton").IsKeyboardFocusWithin, "Image command opens the actual local image overlay and focuses Close.");
        Shot(helpWindow, "S48-c", "help.use", "actual enlargement of the localized document-creation illustration", []);
        RoutedKey(helpWindow, Key.Escape); await Settle(helpWindow);
        Check(!helpVm.IsImageOpen && Math.Abs(helpVm.ScrollOffset - offset) < 2 && imageButton.IsKeyboardFocusWithin, "Escape closes enlargement and restores article position and focus.");
        helpVm.OpenTopic("doc.save"); helpVm.Back.Execute(null); await Settle(helpWindow);
        Check(helpVm.CurrentTopicId == "doc.create" && Math.Abs(helpVm.ScrollOffset - offset) < 2, "History restores the article and reading position.");
        helpVm.Forward.Execute(null); Check(helpVm.CurrentTopicId == "doc.save", "Forward returns to the next article.");
        helpWindow.Close(); service.Open(); await Settle(main);
        helpWindow = Application.Current.Windows.OfType<HelpWindow>().Single(); helpVm = (HelpWindowViewModel)helpWindow.DataContext;
        Check(helpVm.CurrentTopicId == "doc.save", "Reopened help restores the last topic."); helpWindow.Close();
        var after = workspace.CaptureInactiveDocument();
        Check(session.IsDirty && session.FilePath == textPath && new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text == ordinary && after is not null && inactive!.Content.SequenceEqual(after.Content), "Settings and help preserve both dirty documents byte for byte.");
        Check(ai.Messages.Contains(message) && ai.Prompt == "Unsent ORBIT-731", "Settings/help preserve the AI message object and unsent draft without invoking a model.");
        Check(isolated.Paths.All(p => p.StartsWith(isolated.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), "All persistent validation stores remain inside their disposable root.");
        WriteJson("docs/help/screenshots/validation/stage10/scenario-" + culture + ".json", new { culture, commit, checks, checkMessages, updateSource = "Fictional release source, production service/coordinator/view model; no network or installer", bindingErrors, root = isolated.Root });
    }

    private static void Stage10UpdateShot(Window window, Button button, string id, string topic, string state)
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(button);
        while (parent is not null && parent is not Border) parent = VisualTreeHelper.GetParent(parent);
        var bounds = Bounds(window, (Border)parent!);
        Shot(window, id, topic, state, [], new Rect(bounds.X - 8, bounds.Y - 38, bounds.Width + 16, bounds.Height + 46));
    }
}
