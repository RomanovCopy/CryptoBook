using Autofac;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

internal static partial class Program
{
    private static async Task ValidateStage4(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        await isolated.PrepareDemoDriveAsync();
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.WindowState = WindowState.Normal; main.Width = 1440; main.Height = 960;
        var windows = c.Resolve<IWindowManager>();
        var scope = windows.FindHostWindow(((IMainWindowViewModel)main.DataContext).WindowId)!.Scope;
        var nav = scope.Resolve<IPageNavigationService>();
        var session = c.Resolve<IDocumentSession>();
        var workspace = (IWorkspaceDocumentSession)session;
        var editor = c.Resolve<IRichTextBoxService>();
        var menu = scope.Resolve<IMenuFileViewModel>();
        var open = c.Resolve<IWorkspaceFileOpenService>();
        var model = scope.Resolve<IMainWindowModel>();
        nav.Navigate("Home"); await Settle(main);
        var home = (IHomeViewModel)nav.CurrentPage!.DataContext;
        if (!verify) Shot(main, "S03-a", "start.workspace", "step 1", [new(1, Bounds(main, Buttons(main).Single(b => b.Command == home.ChooseWorkspace)))]);
        home.ChooseWorkspace.Execute(null);
        await Until(() => c.Resolve<IWorkspaceService>().WorkspaceDirectory == isolated.DemoDirectory);
        Check(!session.HasDocument, "Choosing a workspace does not create or open a document.");
        c.Resolve<ISettingsWindowService>().Open(); await Settle(main);
        var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().Single();
        settingsWindow.Width = 1120; settingsWindow.Height = 820;
        var settings = (ISettingsViewModel)settingsWindow.DataContext;
        settings.SelectedSectionIndex = 3; await Settle(settingsWindow);
        // Locate the section by the visible ChooseWorkspace command instead of assuming tab order.
        for (int index = 0; index < 6 && !Buttons(settingsWindow).Any(b => b.Command == settings.ChooseWorkspace); index++)
        { settings.SelectedSectionIndex = index; await Settle(settingsWindow); }
        Check(settings.WorkspaceDirectory == isolated.DemoDirectory, "Settings shows the chosen workspace.");
        if (!verify) Shot(settingsWindow, "S03-b", "start.workspace", "step 3", [new(3, Bounds(settingsWindow, Buttons(settingsWindow).Single(b => b.Command == settings.ChooseWorkspace)))]);
        settings.SelectedSectionIndex = 0; await Settle(settingsWindow);
        settings.SelectedDefaultDocumentTemplate = settings.DocumentTemplates.Single(t => t.Id == "meeting");
        settingsWindow.Close();

        home.NewDocument.Execute(null); await Settle(main);
        var dialog = Application.Current.Windows.OfType<NewFileDialog>().Single();
        dialog.Width = 840; dialog.Height = 900;
        var vm = (INewFileDialogViewModel)dialog.DataContext;
        Check(vm.SelectedDocumentTemplate.Id == "meeting", "Default template applies to the next creation dialog.");
        vm.Browse.Execute(null); await Until(() => !vm.IsBusy && vm.TargetDirectory == isolated.DemoDirectory);
        vm.SelectedTemplate = vm.Templates.Single(t => t is RichTextFileTemplate); vm.SelectedNewTemplate.Execute(null);
        string name = Local("Протокол встречи.rtf", "Протокол зустрічі.rtf", "Besprechung.rtf", "Meeting.rtf");
        vm.FileName = name; await Settle(dialog);
        if (!verify) Shot(dialog, "S04-c", "doc.templates", "steps 2-3", [new(2, Bounds(dialog, Named<FrameworkElement>(dialog, "DocumentTemplateSelector"))), new(3, Bounds(dialog, Buttons(dialog).Single(b => b.Command == vm.Create)))]);
        // A once writable directory can disappear while the dialog is open.
        // Valid names still need a real destination write check.
        string unavailable = Path.Combine(isolated.DemoDirectory, "Unavailable");
        Directory.CreateDirectory(unavailable);
        vm.TargetDirectory = unavailable;
        vm.InitSuggested.Execute(unavailable); await Until(() => !vm.IsBusy); await Settle(dialog);
        Directory.Delete(unavailable); File.WriteAllText(unavailable, "Disposable file replacing the destination directory.");
        vm.FileName = name;
        await ((AsyncRelayCommand)vm.Create).ExecuteAsync(); await Settle(dialog);
        Check(dialog.IsVisible && !string.IsNullOrWhiteSpace(vm.ErrorMessage), "Unavailable creation directory leaves the dialog open with an error.");
        if (!verify) Shot(dialog, "S04-d", "doc.create", "unavailable destination", [], new Rect(0, 665, 824, 196));
        vm.Browse.Execute(null); await Until(() => !vm.IsBusy && vm.TargetDirectory == isolated.DemoDirectory);
        vm.FileName = name;
        await ((AsyncRelayCommand)vm.Create).ExecuteAsync(); await Settle(main);
        string original = session.FilePath!; demonstrationFile = original;
        Check(File.Exists(original) && Text(editor.Document).Length > 30, "Meeting template creates and opens populated RTF.");
        byte[] originalBytes = File.ReadAllBytes(original);
        home.NewDocument.Execute(null); await Settle(main);
        dialog = Application.Current.Windows.OfType<NewFileDialog>().Single(); vm = (INewFileDialogViewModel)dialog.DataContext;
        vm.Browse.Execute(null); await Until(() => !vm.IsBusy && vm.TargetDirectory == isolated.DemoDirectory);
        vm.SelectedTemplate = vm.Templates.Single(t => t is RichTextFileTemplate); vm.SelectedNewTemplate.Execute(null);
        vm.FileName = name;
        await ((AsyncRelayCommand)vm.Create).ExecuteAsync(); await Settle(main);
        Check(session.FilePath != original && File.ReadAllBytes(original).SequenceEqual(originalBytes), "Duplicate creation chooses a free name and preserves the existing file.");
        await open.OpenAsync(original); await Settle(main);

        // All six templates in three user-facing formats, with real disk output and reopening.
        foreach (var template in c.Resolve<IDocumentTemplateCatalog>().Templates)
        foreach (IFileTemplate format in new IFileTemplate[] { new RichTextFileTemplate(), new PlainTextTemplate(), new MarkdownFileTemplate() })
        {
            byte[] bytes = await c.Resolve<IDocumentTemplateService>().CreateContentAsync(template, format, DocumentPaperSize.A4, false);
            string path = Path.Combine(isolated.DemoDirectory, template.Id + format.DefaultExtension);
            File.WriteAllBytes(path, bytes);
            Check((await open.OpenAsync(path)).Success, $"Template {template.Id}/{format.Id} opens from disk.");
            string text = format is MarkdownFileTemplate ? c.Resolve<IMarkdownDocumentState>().Text : Text(editor.Document);
            Check(template.Id == "empty" ? string.IsNullOrWhiteSpace(text) : text.Length > 20, $"Template {template.Id}/{format.Id} has expected empty/populated content.");
        }
        await open.OpenAsync(original); nav.Navigate("Home"); await Settle(main);
        var formats = new List<object>();
        foreach (IFileTemplate format in new IFileTemplate[] { new RichTextFileTemplate(), new XamlFileTemplate(), new XamlPackageFileTemplate(), new PlainTextTemplate() })
        {
            editor.Document.Blocks.Clear();
            editor.Document.Blocks.Add(new Paragraph(new Run("Format Ж Є ä") { FontWeight = FontWeights.Bold }));
            session.MarkDirty();
            string destination = Path.Combine(isolated.DemoDirectory, "roundtrip" + format.DefaultExtension);
            isolated.SavePicker.Targets.Enqueue(new DocumentSaveTarget(destination, format));
            await ((AsyncRelayCommand)menu.SaveAsFile).ExecuteAsync();
            Check(session.FilePath == destination && !session.IsDirty && File.Exists(destination), $"Save As {format.Id} changes the active path after writing.");
            await open.OpenAsync(original);
            Check((await open.OpenAsync(destination)).Success && Text(editor.Document).Contains("Format Ж Є ä"), $"Reopened {format.Id} retains multilingual text.");
            var paragraph = (Paragraph)editor.Document.Blocks.FirstBlock;
            var weight = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).GetPropertyValue(TextElement.FontWeightProperty);
            bool bold = weight is FontWeight w && w == FontWeights.Bold;
            Check(bold == (format is RichTextFileTemplate or XamlPackageFileTemplate), $"{format.Id}: bold is {(bold ? "retained" : "lost")} after reopening.");
            byte[] bytes = File.ReadAllBytes(destination);
            formats.Add(new { format = format.Id, extension = format.DefaultExtension, bold, bytePrefix = Convert.ToHexString(bytes.AsSpan(0, Math.Min(12, bytes.Length))) });
        }
        File.WriteAllText(Path.Combine(isolated.Root, "formats.json"), JsonSerializer.Serialize(formats, Json));
        await open.OpenAsync(original); nav.Navigate("Home"); await Settle(main);
        editor.Document.Blocks.Add(new Paragraph(new Run(Local("Несохранённый обычный текст.", "Незбережений звичайний текст.", "Ungespeicherter normaler Text.", "Unsaved ordinary text.")))); session.MarkDirty();
        string dirtyText = Text(editor.Document); long revision = session.Revision;
        isolated.SavePicker.Targets.Enqueue(null);
        await ((AsyncRelayCommand)menu.SaveAsFile).ExecuteAsync();
        Check(session.FilePath == original && session.IsDirty && session.Revision == revision && Text(editor.Document) == dirtyText, "Cancel Save As preserves path, dirty revision and text.");
        model.IsMenuOpen = true; await Settle(main);
        if (!verify) Shot(main, "S06-c", "doc.save", "Save As entry", [new(1, Bounds(main, Buttons(main).Single(b => b.Command == menu.SaveAsFile)))], new Rect(0, 40, 920, 850));
        if (!verify) Shot(main, "S06-e", "doc.save", "Save step 2", [new(2, Bounds(main, Buttons(main).Last(b => b.Command == menu.SaveFile)))], new Rect(0, 40, 920, 850));
        model.IsMenuOpen = false;

        byte[] beforeFailure = File.ReadAllBytes(original);
        using (var locked = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using var dismiss = OnVisible<MessageWindow>(async window =>
            {
                await Settle(window);
                Check(Visuals<TextBlock>(window).Any(t => t.Text == LocalizationManager.GetString("Document.FileInUse")), "Sharing violation displays the localized actionable message.");
                if (!verify) Shot(window, "S06-d", "doc.save", "write error", []);
                window.Close();
            });
            await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
        }
        Check(session.IsDirty && session.FilePath == original && Text(editor.Document) == dirtyText && File.ReadAllBytes(original).SequenceEqual(beforeFailure), "Sharing violation preserves the original file and unsaved editor text.");
        using (var locked = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var dismiss = OnVisible<MessageWindow>(window => { window.Close(); return Task.CompletedTask; }))
        {
            isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Save);
            await ((AsyncRelayCommand)menu.CloseFile).ExecuteAsync();
            Check(session.FilePath == original && session.IsDirty && Text(editor.Document) == dirtyText, "Save failure prevents Close and retains the edited document.");
        }
        await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
        Check(!session.IsDirty && File.ReadAllBytes(original).Length > 0, "Retry Save succeeds after the lock is released.");

        // File manager and genuine SelectFolder picker are distinct production modes.
        c.Resolve<IFileExplorerService>().Show(isolated.DemoDirectory); await Settle(main);
        var explorer = Application.Current.Windows.OfType<FileExplorer>().Single(); explorer.Width = 1280; explorer.Height = 850;
        var explorerVm = (IFileExplorerViewModel)explorer.DataContext;
        await Until(() => explorerVm.ChildrenView.Cast<object>().Any()); await Settle(explorer);
        Check(!explorerVm.IsPickerMode, "Open file command uses Manage mode.");
        var fileItem = explorerVm.ChildrenView.Cast<ISystemItem>().OfType<IFileItem>().Single(item => Path.GetFileName(item.FullPath) == Path.GetFileName(original));
        var fileList = Visuals<ListView>(explorer).Single();
        fileList.SelectedItem = fileItem; fileList.ScrollIntoView(fileItem);
        explorerVm.SelectedListItem = fileItem; explorerVm.SelectedItemsSnapshot = [fileItem]; await Settle(explorer);
        if (!verify) Shot(explorer, "S05-a", "doc.open", "file manager step 3", [new(3, Bounds(explorer, Buttons(explorer).First(b => b.Command == explorerVm.OpenCommand)))]);
        await ((AsyncRelayCommand)menu.CloseFile).ExecuteAsync();
        Check(explorerVm.OpenCommand.CanExecute(fileItem), "File manager Open is available for the selected file.");
        explorerVm.OpenCommand.Execute(fileItem);
        await Until(() => session.FilePath == original);
        Check(session.HasDocument && Text(editor.Document).Contains(dirtyText.Trim()), "File manager Open loads the selected file through the production command.");
        explorer.Close();
        using (var choose = OnVisible<FileExplorer>(async window =>
        {
            window.Width = 1280; window.Height = 850; await Settle(window);
            var picker = (IFileExplorerViewModel)window.DataContext;
            Check(picker.IsPickerMode && picker.ConfirmSelectionCommand.CanExecute(null), "SelectFolder offers the current demo folder.");
            if (!verify) Shot(window, "S05-b", "start.workspace", "step 2", [new(2, Bounds(window, Buttons(window).Single(b => b.Command == picker.ConfirmSelectionCommand)))]);
            picker.ConfirmSelectionCommand.Execute(null);
        }))
        {
            string? folder = await new CryptoBook.Services.FileExplorerService(windows, c.Resolve<IFileManagerService>()).PickFolderAsync(isolated.DemoDirectory, default);
            Check(folder == isolated.DemoDirectory, "Real folder picker confirms the selected demo directory.");
        }
        using (var cancel = OnVisible<FileExplorer>(window =>
        {
            ((IFileExplorerViewModel)window.DataContext).CancelSelectionCommand.Execute(null); return Task.CompletedTask;
        }))
        {
            string? folder = await new CryptoBook.Services.FileExplorerService(windows, c.Resolve<IFileManagerService>()).PickFolderAsync(isolated.DemoDirectory, default);
            Check(folder is null && session.FilePath == original, "Cancel folder selection returns no path and keeps the document.");
        }

        // Independent dirty documents; source bytes/BOM/newlines are authoritative for Markdown.
        string mdPath = Path.Combine(isolated.DemoDirectory, "separate.md");
        string source = "# Markdown\r\n\r\nЖ Є ä\r\n";
        var encoding = new UnicodeEncoding(false, true);
        File.WriteAllBytes(mdPath, encoding.GetPreamble().Concat(encoding.GetBytes(source)).ToArray());
        editor.Document.Blocks.Add(new Paragraph(new Run(Local("Текст обычного документа.", "Текст звичайного документа.", "Text des normalen Dokuments.", "Ordinary document text.")))); session.MarkDirty();
        string ordinaryText = Text(editor.Document);
        Check((await open.OpenAsync(mdPath)).Success, "Opening Markdown parks the dirty ordinary document without a replacement prompt.");
        var md = c.Resolve<IMarkdownDocumentState>();
        md.Text += Local("Несохранённый Markdown.", "Незбережений Markdown.", "Ungespeichertes Markdown.", "Unsaved Markdown.");
        string markdownText = md.Text;
        model.IsMenuOpen = true; await Settle(main);
        if (!verify) Shot(main, "S07-b", "doc.switch", "Markdown document", []);
        nav.Navigate("Home"); await Settle(main);
        Check(session.FilePath == original && session.IsDirty && Text(editor.Document) == ordinaryText, "Returning Home restores the ordinary document and dirty state.");
        if (!verify) Shot(main, "S07-a", "doc.switch", "ordinary document", []);
        nav.Navigate("MarkdownEditor"); await Settle(main); model.IsMenuOpen = false;
        Check(md.Text == markdownText && session.IsDirty && session.FilePath == mdPath, "Returning Markdown restores its own source, path and dirty state.");
        await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
        Check(File.ReadAllBytes(mdPath).SequenceEqual(encoding.GetPreamble().Concat(encoding.GetBytes(markdownText))), "Markdown Save preserves UTF-16 LE BOM, Unicode and CRLF source bytes.");
        Check(workspace.HasInactiveChanges, "Saving Markdown leaves the ordinary document dirty.");
        md.Text += "\r\n";
        nav.Navigate("Home"); await Settle(main);
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Discard);
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Cancel);
        Check(!await c.Resolve<IUnsavedChangesGuard>().CanCloseAsync() && session.FilePath == original && workspace.HasInactiveChanges,
            "Application close checks the inactive dirty document and restores the active editor after Cancel.");
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Cancel);
        Check((await open.OpenAsync(Path.Combine(isolated.DemoDirectory, "notes.rtf"))).Cancelled && session.FilePath == original && Text(editor.Document) == ordinaryText, "Cancel replacement retains the dirty ordinary document.");
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Cancel);
        await ((AsyncRelayCommand)menu.CloseFile).ExecuteAsync();
        Check(session.HasDocument && session.IsDirty, "Cancel Close retains the active dirty document.");
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Save);
        await ((AsyncRelayCommand)menu.CloseFile).ExecuteAsync();
        Check(!workspace.HasHomeDocument && workspace.HasMarkdownDocument && File.ReadAllBytes(original).Length > beforeFailure.Length, "Save and Close writes the ordinary document while retaining Markdown.");
        await open.OpenAsync(original); await Settle(main);
        editor.Document.Blocks.Add(new Paragraph(new Run("Discard this change."))); session.MarkDirty();
        byte[] beforeDiscard = File.ReadAllBytes(original);
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Discard);
        await ((AsyncRelayCommand)menu.CloseFile).ExecuteAsync();
        Check(!workspace.HasHomeDocument && File.ReadAllBytes(original).SequenceEqual(beforeDiscard), "Discard and Close does not write unsaved changes.");
        await open.OpenAsync(original); await Settle(main);

        // Rename through the actual side-menu command and TextInputDialog.
        model.IsMenuOpen = true; await Settle(main);
        var renameButton = Buttons(main).Single(b => Visuals<TextBlock>(b).Any(t => t.Text == LocalizationManager.GetString("SideMenu.RenameBook")));
        using (var rename = OnVisible<TextInputDialog>(async window =>
        {
            await Settle(window);
            ((TextBox)window.FindName("ValueTextBox")).Text = Local("Итоги встречи", "Підсумки зустрічі", "Besprechungsergebnis", "Meeting results");
            if (!verify) Shot(window, "S07-c", "doc.switch", "rename", []);
            Buttons(window).Single(b => b.IsDefault).Command.Execute(null);
        })) await ((AsyncRelayCommand)renameButton.Command).ExecuteAsync();
        Check(session.FilePath != original && File.Exists(session.FilePath) && !File.Exists(original) && Path.GetExtension(session.FilePath) == ".rtf", "Rename book changes the file path and preserves its extension.");
        model.IsMenuOpen = false;

        bool firstEncoding = true;
        foreach (Encoding sourceEncoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true),
            new UnicodeEncoding(false, true), new UnicodeEncoding(true, true), new UTF32Encoding(false, true), new UTF32Encoding(true, true) })
        {
            string path = Path.Combine(isolated.DemoDirectory, "encoding-" + sourceEncoding.CodePage + "-" + sourceEncoding.GetPreamble().Length + ".md");
            string text = "# Ж Є ä\r\n\r\n";
            File.WriteAllBytes(path, sourceEncoding.GetPreamble().Concat(sourceEncoding.GetBytes(text)).ToArray());
            if (firstEncoding) { isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Discard); firstEncoding = false; }
            Check((await open.OpenAsync(path)).Success, "Markdown opens " + sourceEncoding.WebName);
            md.Text += "End\r\n";
            await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
            Check(File.ReadAllBytes(path).SequenceEqual(sourceEncoding.GetPreamble().Concat(sourceEncoding.GetBytes(text + "End\r\n"))),
                "Markdown roundtrip bytes/BOM/CRLF: " + sourceEncoding.WebName + "/" + sourceEncoding.GetPreamble().Length);
        }

        if (verify)
        {
            c.Resolve<IHelpWindowService>().Open(); await Settle(main);
            var help = Application.Current.Windows.OfType<HelpWindow>().Single();
            var helpVm = (HelpWindowViewModel)help.DataContext;
            var content = c.Resolve<HelpContentService>();
            help.Width = 720; help.Height = 480;
            foreach (string id in new[] { "start.interface", "start.workspace", "doc.create", "doc.templates", "doc.open", "doc.save", "doc.formats", "doc.switch" })
            {
                Check(helpVm.OpenTopic(id) && !string.IsNullOrWhiteSpace(Text(helpVm.Article)), "Embedded help renders " + id);
                var topic = content.Find(culture, id)!;
                foreach (string path in topic.Images) Check(content.LoadImage(culture, topic, path) is not null, "Embedded help image " + id + "/" + path);
                await Settle(help);
                var articleScroll = Visuals<ScrollViewer>((FlowDocumentScrollViewer)help.FindName("ArticleViewer")).First();
                Check(articleScroll.ExtentWidth <= articleScroll.ViewportWidth + 1, "Narrow help reflows text, tables and images: " + id);
                if (id == "doc.formats") SaveWindow(help, Path.Combine(isolated.Root, "stage4-formats-narrow.png"));
            }
            help.Width = 1120; help.Height = 780;
            helpVm.OpenTopic("doc.save");
            await Settle(help);
            SaveWindow(help, Path.Combine(isolated.Root, "stage4-help.png")); help.Close();
        }
        Check(isolated.SavePicker.Targets.Count == 0 && isolated.DocumentDialogs.Choices.Count == 0, "All supplied native-dialog decisions were consumed.");
        Check(isolated.Paths.All(p => p.StartsWith(isolated.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), "All registered stores remain isolated.");
    }

    private static string Text(FlowDocument document) => new TextRange(document.ContentStart, document.ContentEnd).Text;
    private static string Local(string ru, string uk, string de, string en) => culture switch { "ru-RU" => ru, "uk-UA" => uk, "de-DE" => de, _ => en };
    private static async Task Until(Func<bool> ready)
    { for (int n = 0; n < 200 && !ready(); n++) await Task.Delay(25); Check(ready(), "Asynchronous UI operation completed."); }
    private sealed class DialogWatch(DispatcherTimer timer) : IDisposable { public void Dispose() => timer.Stop(); }
    private static IDisposable OnVisible<T>(Func<T, Task> action, int intervalMs = 150) where T : Window
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        timer.Tick += async (_, _) =>
        {
            var window = Application.Current.Windows.OfType<T>().FirstOrDefault(w => w.IsVisible);
            if (window is null) return;
            timer.Stop();
            try { await action(window); }
            catch (Exception e) { Console.Error.WriteLine(e); window.Close(); throw; }
        };
        timer.Start(); return new DialogWatch(timer);
    }
}
