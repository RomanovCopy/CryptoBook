using Autofac;
using CryptoBook.Behaviors;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MenuItem = System.Windows.Controls.MenuItem;

internal static partial class Program
{
    private static readonly string[] Stage5Topics = ["edit.basic", "edit.font", "edit.palette", "edit.paragraph", "edit.links", "edit.page", "edit.view",
        "content.images", "content.background", "content.tables", "content.bookmarks", "content.structure", "md.edit", "md.syntax", "md.preview", "md.save"];

    private static async Task ValidateStage5(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.WindowState = WindowState.Normal; main.Width = 1440; main.Height = 960;
        var windows = c.Resolve<IWindowManager>();
        var scope = windows.FindHostWindow(((IMainWindowViewModel)main.DataContext).WindowId)!.Scope;
        var nav = scope.Resolve<IPageNavigationService>();
        var editor = c.Resolve<IRichTextBoxService>();
        var session = c.Resolve<IDocumentSession>();
        var menu = scope.Resolve<IMenuFileViewModel>();
        var opener = c.Resolve<IWorkspaceFileOpenService>();
        string path = Path.Combine(isolated.DemoDirectory, "Editor.XamlPackage");
        var initial = new FlowDocument(new Paragraph(new Run(Local("Учебный документ", "Навчальний документ", "Übungsdokument", "Practice document")))) { PageWidth = 793.7, PageHeight = double.NaN };
        editor.ReplaceDocument(initial);
        await c.Resolve<IFlowDocumentSaveService>().SaveToFileAsync(editor, path, new XamlPackageFileTemplate());
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Discard);
        Check((await opener.OpenAsync(path)).Success, "A disposable XamlPackage opens in the production editor.");
        demonstrationFile = path; nav.Navigate("Home"); await Settle(main);
        var fonts = (IFontFormatBar_ViewModel)Visuals<CryptoBook.MyControls.FontFormatBar>(main).Single().DataContext;
        var paragraphs = (ITextFormatBarViewModel)Visuals<CryptoBook.MyControls.TextFormatBar>(main).Single().DataContext;
        var lists = (IListFormatBarViewModel)Visuals<CryptoBook.MyControls.ListFormatBar>(main).Single().DataContext;
        var context = (IRichTextContextMenuViewModel)editor.Service.ContextMenu.DataContext;
        var home = (IHomeViewModel)nav.CurrentPage!.DataContext;
        var view = home.DocumentView;
        var first = (Paragraph)editor.Document.Blocks.FirstBlock!;
        void SelectFirst() { editor.Focus(); editor.Selection.Select(first.ContentStart, first.ContentEnd); }
        SelectFirst(); fonts.SetFontWeightCommand.Execute(FontWeights.Bold);
        Check((FontWeight)editor.Selection.GetPropertyValue(TextElement.FontWeightProperty) == FontWeights.Bold, "Font weight command formats the selected text.");
        fonts.SetFontStyleCommand.Execute(FontStyles.Italic);
        fonts.SetFontStretchCommand.Execute(FontStretches.Expanded);
        fonts.SetFontFamilyCommand.Execute(new FontFamily("Calibri"));
        fonts.SetFontSizeCommand.Execute(20d);
        fonts.SetFontColorCommand.Execute(System.Drawing.Color.DarkBlue);
        fonts.SetFontBackgroundCommand.Execute(System.Drawing.Color.LightYellow);
        Check((double)editor.Selection.GetPropertyValue(TextElement.FontSizeProperty) == 20, "Font size command formats the selected text.");
        await PopupShot<CryptoBook.MyControls.FontFormatBar>(main, "S08-b", "edit.font", verify);
        fonts.ClearFormattingCommand.Execute(null);
        Check((FontWeight)editor.Selection.GetPropertyValue(TextElement.FontWeightProperty) != FontWeights.Bold, "Clear formatting resets the selected font.");
        fonts.SetFontWeightCommand.Execute(FontWeights.Bold);
        paragraphs.SetTextAlignment.Execute(TextAlignment.Center);
        paragraphs.SetParagraphIndent.Execute("20");
        paragraphs.SetLineHeight.Execute(1d);
        Check(first.TextAlignment == TextAlignment.Center && first.TextIndent == 20 && first.LineHeight > 0, "Alignment, first-line indent and line spacing commands apply.");
        await PopupShot<CryptoBook.MyControls.TextFormatBar>(main, "S09-a", "edit.paragraph", verify);
        lists.ToggleBulleted.Execute(null);
        Check(first.Parent is System.Windows.Documents.ListItem, "Bulleted list command wraps the paragraph.");
        lists.ToggleNumbered.Execute(null);
        await PopupShot<CryptoBook.MyControls.ListFormatBar>(main, "S09-b", "edit.paragraph", verify);
        lists.ClearLists.Execute(null);
        Check(first.Parent is FlowDocument, "Clear list command restores a paragraph.");
        SelectFirst();
        var palette = (IInkPaletteViewModel)Visuals<CryptoBook.MyControls.InkPalette>(main).Single().DataContext; var slot = palette.Colors[0];
        slot.Choices.First(choice => choice.Color.ToArgb() == System.Drawing.Color.DarkGreen.ToArgb()).ChooseCommand.Execute(null);
        slot.ApplyCommand.Execute(null);
        Check(((SolidColorBrush)editor.Selection.GetPropertyValue(TextElement.ForegroundProperty)).Color.G == System.Drawing.Color.DarkGreen.G, "Changing an ink slot and applying it are separate commands.");
        var paletteControl = Visuals<CryptoBook.MyControls.InkPalette>(main).First();
        var colorButton = Visuals<Button>(paletteControl).First();
        colorButton.ContextMenu.PlacementTarget = colorButton; colorButton.ContextMenu.IsOpen = true; await Settle(main);
        if (!verify) Shot(main, "S09-c", "edit.palette", "slot colour choices", [], surface: colorButton.ContextMenu);
        colorButton.ContextMenu.IsOpen = false;

        // Exercise clipboard commands only with disposable text and restore the prior data object in memory.
        var previousClipboard = Clipboard.GetDataObject();
        try
        {
            SelectFirst(); string selected = editor.Selection.Text;
            editor.Copy(); Check(Clipboard.GetText() == selected, "Copy places the selected disposable text on the clipboard.");
            editor.Cut(); Check(!Text(editor.Document).Contains(selected.Trim()), "Cut removes the selected disposable text.");
            editor.Paste(); Check(Text(editor.Document).Contains(selected.Trim()), "Paste restores the disposable text.");
        }
        finally { if (previousClipboard is null) Clipboard.Clear(); else Clipboard.SetDataObject(previousClipboard, true); }
        string before = Text(editor.Document); editor.InsertTextAtCaret("UNDO"); editor.Undo();
        Check(Text(editor.Document) == before, "Undo restores text before insertion."); editor.Redo(); Check(Text(editor.Document).Contains("UNDO"), "Redo reapplies text insertion."); editor.Undo();
        editor.Document.Blocks.Add(new Paragraph());
        context.RemoveEmptyParagraphs.Execute(null); Check(!editor.HasEmptyParagraphs(), "Remove empty paragraphs command removes empty paragraphs.");
        editor.Focus(); editor.Service.ContextMenu.IsOpen = true; await Settle(main);
        if (!verify) Shot(main, "S08-a", "edit.basic", "editor context menu", [], surface: editor.Service.ContextMenu);
        editor.Service.ContextMenu.IsOpen = false;

        // Real modal HyperlinkDialog, including invalid address and cancellation before insertion.
        SelectFirst();
        Task response = Respond<HyperlinkDialog>(async dialog =>
        {
            dialog.Url = "https://example.com"; dialog.DisplayText = Local("Пример", "Приклад", "Beispiel", "Example");
            dialog.DataContext = null; dialog.DataContext = dialog; await Settle(dialog);
            if (!verify) Shot(dialog, "S10-a", "edit.links", "address and display text", []);
            Buttons(dialog).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        });
        paragraphs.InsertHyperlink.Execute(null); await response;
        Check(Descendants(editor.Document).OfType<Hyperlink>().Any(h => h.NavigateUri?.Host == "example.com"), "Link dialog inserts the requested HTTP hyperlink.");
        before = Text(editor.Document);
        response = Respond<HyperlinkDialog>(dialog => { Buttons(dialog).Single(b => b.IsCancel).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return Task.CompletedTask; });
        paragraphs.InsertHyperlink.Execute(null); await response;
        Check(Text(editor.Document) == before, "Cancelling the link dialog preserves content.");

        string imagePath = Path.Combine(isolated.DemoDirectory, "illustration.png");
        var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen())
        { dc.DrawRectangle(Brushes.LightSkyBlue, null, new Rect(0, 0, 480, 280)); dc.DrawEllipse(Brushes.Gold, null, new Point(330, 70), 38, 38); dc.DrawRectangle(Brushes.SeaGreen, null, new Rect(0, 200, 480, 80)); }
        var bitmap = new RenderTargetBitmap(480, 280, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing); WriteBitmap(bitmap, imagePath);
        editor.CaretPosition = editor.Document.ContentEnd;
        var content = scope.Resolve<IMenuContentViewModel>();
        isolated.ImagePicker.Paths.Enqueue(null); before = Text(editor.Document);
        await ((AsyncRelayCommand)content.InsertImage).ExecuteAsync(); Check(Text(editor.Document) == before, "Cancelling image selection preserves content.");
        isolated.ImagePicker.Paths.Enqueue(imagePath); await ((AsyncRelayCommand)content.InsertImage).ExecuteAsync(); await Settle(main);
        var image = Visuals<Image>(editor.Service).Single(i => i.Source is BitmapSource);
        c.Resolve<IEmbeddedImageEditor>().ResizeToWidth(image, 260, 700);
        Check(Math.Abs(image.Width - 260) < 1, "Embedded image width changes with its aspect ratio.");
        var layout = c.Resolve<IEmbeddedImageLayoutService>();
        int modeIndex = 0;
        foreach (var mode in Enum.GetValues<ImageLayoutMode>())
        {
            layout.SetLayout(image, mode); Check(layout.GetLayout(image) == mode, "Image layout " + mode + " applies."); await Settle(main);
            image.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });
            await Settle(main);
            if (!verify) Shot(main, "S12-" + (char)('a' + modeIndex++), "content.images", "layout " + mode, [new(2, Bounds(main, image))], new Rect(0, 0, 1440, 650));
        }
        if (layout.CanMoveUp(image)) Check(layout.MoveUp(image), "Image moves above its current block.");
        if (layout.CanMoveDown(image)) Check(layout.MoveDown(image), "Image moves below its current block.");
        editor.BeginChange();
        bool removedImage;
        try { removedImage = layout.Remove(image); } finally { editor.EndChange(); }
        Check(removedImage, "Remove image removes the embedded image, not its source file."); editor.Undo();
        await Settle(main);
        bool imageUndoRestoredBitmap = Visuals<Image>(editor.Service).Any(i => i.Source is BitmapSource);
        WriteJson(Path.Combine(isolated.Root, "image-undo.json"), new { imageUndoRestoredBitmap,
            operation = "FloatRight image moved up/down then removed via production layout service inside editor change block, followed by Undo",
            limitation = "Do not promise recovery of an embedded bitmap through Undo; keep the original image or a saved document copy." });
        if (!imageUndoRestoredBitmap)
        { editor.CaretPosition = editor.Document.ContentEnd; await c.Resolve<IDocumentImageInserter>().InsertAsync(bitmap); }
        Check(File.Exists(imagePath), "Removing an embedded image preserves its source file.");

        var table = new Table(); table.Columns.Add(new TableColumn()); table.Columns.Add(new TableColumn());
        var group = new TableRowGroup(); table.RowGroups.Add(group);
        for (int r = 0; r < 2; r++) { var row = new TableRow(); group.Rows.Add(row); for (int col = 0; col < 2; col++) row.Cells.Add(new TableCell(new Paragraph(new Run($"{r + 1}:{col + 1}")))); }
        editor.Document.Blocks.Add(table); editor.CaretPosition = group.Rows[0].Cells[0].ContentStart;
        var tables = c.Resolve<ITableEditingService>();
        Check(tables.CanEdit, "A caret in one rectangular table cell enables structure commands.");
        foreach (var operation in Enum.GetValues<TableEditOperation>())
        { editor.CaretPosition = group.Rows[0].Cells[0].ContentStart; Check(tables.Edit(operation), "Table operation " + operation + " succeeds."); }
        editor.CaretPosition = group.Rows[0].Cells[0].ContentStart;
        editor.Service.ContextMenu.IsOpen = true; await Settle(main);
        var tableItem = editor.Service.ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, LocalizationManager.GetString("Editor.Table")));
        tableItem.IsSubmenuOpen = true; await Settle(main);
        if (!verify) Shot(main, "S14-a", "content.tables", "six table structure commands", [], surface: (FrameworkElement)tableItem.Template.FindName("PART_Popup", tableItem) is Popup p ? (FrameworkElement)p.Child : editor.Service.ContextMenu);
        editor.Service.ContextMenu.IsOpen = false;
        group.Rows[0].Cells[0].ColumnSpan = 2; Check(!tables.CanEdit, "Merged cells disable table structure commands."); group.Rows[0].Cells[0].ColumnSpan = 1;

        var bookmarks = (IBookmarksViewModel)Visuals<CryptoBook.MyControls.BookmarksBar>(main).Single().DataContext; editor.CaretPosition = editor.Document.ContentStart;
        bookmarks.NewBookmarkName = Local("Начало", "Початок", "Anfang", "Start"); bookmarks.AddAtCaret.Execute(null);
        Check(bookmarks.Bookmarks.Count == 1, "Add bookmark creates an anchor at the caret.");
        bookmarks.SelectedBookmark = bookmarks.Bookmarks[0]; bookmarks.RenameTo = Local("Введение", "Вступ", "Einleitung", "Introduction"); bookmarks.Rename.Execute(null);
        Check(bookmarks.SelectedBookmark!.Name == bookmarks.RenameTo, "Rename bookmark updates its visible name.");
        bookmarks.SelectedBookmark!.Note = Local("Учебная заметка", "Навчальна нотатка", "Übungsnotiz", "Practice note");
        bookmarks.NavigateTo.Execute(null); bookmarks.NextBookmark.Execute(null); bookmarks.PreviousBookmark.Execute(null);
        var linkParagraph = new Paragraph(new Run(" ")); editor.Document.Blocks.Add(linkParagraph);
        editor.CaretPosition = linkParagraph.ContentStart; bookmarks.LinkText = bookmarks.SelectedBookmark.Name;
        Check(bookmarks.InsertHyperlinkTo.CanExecute(null), "Bookmark link is available at a valid paragraph caret.");
        bookmarks.InsertHyperlinkTo.Execute(null);
        Check(c.Resolve<IFlowDocumentWalker>().Find<Hyperlink>(editor.Document).Any(h => h.NavigateUri?.OriginalString.StartsWith('#') == true), "Insert bookmark link creates an internal destination.");
        await PopupShot<CryptoBook.MyControls.BookmarksBar>(main, "S15-a", "content.bookmarks", verify);
        bookmarks.OpenManager.Execute(null); await Settle(main);
        var manager = Application.Current.Windows.OfType<BookmarksEditor>().Single();
        manager.Width = 850; manager.Height = 520;
        var managerVm = (IBookmarksEditorViewModel)manager.DataContext;
        managerVm.SelectedBookmark = managerVm.Bookmarks[0]; await Settle(manager);
        if (!verify) Shot(manager, "S15-b", "content.bookmarks", "bookmark manager and note", []); manager.Close();
        bookmarks.RebuildIndexFromDocument.Execute(null); Check(bookmarks.Bookmarks.Count == 1, "Bookmark index rebuild retains the live anchor.");
        int anchorsWithoutBackground;
        using (var package = new MemoryStream())
        {
            await c.Resolve<IFlowDocumentSaveService>().SaveToStreamAsync(editor, package, new XamlPackageFileTemplate());
            package.Position = 0;
            var loaded = await c.Resolve<IFlowDocumentLoadService>().PrepareAsync(package, new XamlPackageFileTemplate());
            anchorsWithoutBackground = c.Resolve<IFlowDocumentWalker>().Find<Span>(loaded).Count(s => s.Name.StartsWith("Bookmark_", StringComparison.Ordinal));
        }
        var structure = home.DocumentStructure; structure.ToggleCommand.Execute(null); structure.IncludeTextElements = true; structure.RefreshCommand.Execute(null);
        Check(structure.Nodes.Count > 0, "Structure tree contains the current document.");
        var node = Flatten(structure.Nodes).First(n => n.Source is Paragraph && n.CanDelete);
        structure.NavigateCommand.Execute(node); structure.AddParagraphAfterCommand.Execute(node); structure.RefreshCommand.Execute(null); await Settle(main);
        Check(Visuals<TreeView>(main).Any(t => t.IsVisible), "The production structure tree is visible in Home.");
        if (!verify) Shot(main, "S16-a", "content.structure", "expanded document structure", []);
        view.ToggleView.Execute(null); await Settle(main);
        Check(Visuals<FlowDocumentPageViewer>(main).Any(v => v.IsVisible), "The production paginated preview is visible.");
        Check(view.IsPreviewMode && !structure.IsEditingEnabled && !structure.NavigateCommand.CanExecute(node), "Reading mode disables structure navigation and editing.");
        if (!view.IsFitToWindow) view.ToggleFitToWindow.Execute(null); Check(view.IsFitToWindow, "Fit page command applies in reading mode.");
        if (!verify) Shot(main, "S11-a", "edit.view", "reading mode and fit page", []);
        view.ToggleView.Execute(null); await Settle(main); structure.ToggleCommand.Execute(null);
        await Settle(main);
        DocumentPageBorderBehavior.ResetZoomCommand.Execute(null, editor.Service);
        await Settle(main);
        Check(editor.Service.LayoutTransform is ScaleTransform scale && Math.Abs(scale.ScaleX - 1) < .01, "100 percent command resets edit zoom.");

        isolated.ImagePicker.Paths.Enqueue(imagePath); await ((AsyncRelayCommand)fonts.ChooseDocumentBackgroundImageCommand).ExecuteAsync();
        var fontService = c.Resolve<IFontService>(); Check(fontService.HasDocumentBackgroundImage, "Background image picker applies the disposable image.");
        fontService.SetDocumentBackgroundImageCrop(new Rect(.1, .1, .8, .8));
        await PopupShot<CryptoBook.MyControls.FontFormatBar>(main, "S13-a", "content.background", verify);
        session.MarkDirty(); byte[] prior = File.ReadAllBytes(path);
        response = Respond<MessageWindow>(async dialog => { await Settle(dialog); if (!verify) Shot(dialog, "S13-b", "content.background", "crop finalization warning", []); ((IMessageWindowViewModel)dialog.DataContext).CancelCommand.Execute(null); });
        await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync(); await response;
        Check(prior.SequenceEqual(File.ReadAllBytes(path)) && session.IsDirty, "Declining crop finalization preserves the file and dirty document.");
        response = Respond<MessageWindow>(dialog => { ((IMessageWindowViewModel)dialog.DataContext).OkCommand.Execute(null); return Task.CompletedTask; });
        await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync(); await response;
        Check(!session.IsDirty && DocumentBackgroundImageLayout.IsFinalized((ImageBrush)editor.Document.Background), "Successful Save finalizes the background crop.");
        fontService.ClearDocumentBackgroundImage(); Check(!fontService.HasDocumentBackgroundImage, "Remove background restores a solid background.");
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Discard);
        string other = Path.Combine(isolated.DemoDirectory, "other.txt"); File.WriteAllText(other, "Other disposable document.");
        await opener.OpenAsync(other);
        await opener.OpenAsync(path); // saved file contains the image crop, table, text and bookmark
        var walker = c.Resolve<IFlowDocumentWalker>();
        Check(editor.Document.Background is ImageBrush, "XamlPackage reopening retains the finalized background.");
        Check(walker.Find<Table>(editor.Document).Any() && walker.Find<Hyperlink>(editor.Document).Any(), "XamlPackage reopening retains table and hyperlinks.");
        Check(walker.Find<InlineUIContainer>(editor.Document).Any(i => i.Child is Image) || walker.Find<BlockUIContainer>(editor.Document).Any(i => i.Child is Image), "XamlPackage reopening retains the embedded image.");
        bookmarks.RebuildIndexFromDocument.Execute(null);
        WriteJson(Path.Combine(isolated.Root, "bookmark-roundtrip.json"), new { beforeSave = 1, anchorsWithoutBackground, afterReopen = bookmarks.Bookmarks.Count,
            namesAfterReopen = bookmarks.Bookmarks.Select(b => b.Name).ToArray(),
            format = "XamlPackage", operation = "Save with background finalization, open another file, reopen saved package and rebuild bookmark index" });
        FlowDocument ordinaryDocument = editor.Document;
        string ordinaryText = Text(ordinaryDocument);
        Brush ordinaryBackground = ordinaryDocument.Background;

        string mdPath = Path.Combine(isolated.DemoDirectory, "Examples.md");
        File.Copy(imagePath, Path.Combine(isolated.DemoDirectory, "image.png"));
        Directory.CreateDirectory(Path.Combine(isolated.DemoDirectory, "images"));
        File.Copy(imagePath, Path.Combine(isolated.DemoDirectory, "images", "picture.png"));
        var syntaxArticle = c.Resolve<HelpContentService>().Find(culture, "md.syntax")!;
        var syntaxBlocks = Markdig.Markdown.Parse(syntaxArticle.Markdown).OfType<Markdig.Syntax.FencedCodeBlock>().ToArray();
        Check(syntaxBlocks.Length == 40, "Syntax article retains all forty legacy examples.");
        var renderedExamples = new List<object>();
        foreach (var example in syntaxBlocks.Skip(5))
        {
            string source = example.Lines.ToString().Replace("  ⏎", "  ");
            FlowDocument rendered = c.Resolve<IMarkdownFlowDocumentRenderer>().Render(source, mdPath);
            renderedExamples.Add(new { source, visibleText = Text(rendered), elementTypes = walker.Traverse(rendered).Select(e => e.GetType().Name).Distinct().ToArray() });
        }
        WriteJson(Path.Combine(isolated.Root, "markdown-examples.json"), new { culture, editorExamples = 5, renderedExamples });
        Check(renderedExamples.Count == 35, "All thirty-five syntax and limitation examples render without invoking external navigation.");
        string heading = Local("Примеры Markdown", "Приклади Markdown", "Markdown-Beispiele", "Markdown examples");
        string mdText = $"# {heading}\r\n\r\n**Bold** / *italic* / ~~strike~~\r\n\r\n> Quote\r\n\r\n- First\r\n- Second\r\n\r\n| A | B |\r\n|---|---|\r\n| 1 | 2 |\r\n\r\n![Landscape](illustration.png)\r\n\r\n```text\r\ncode\r\n```\r\n";
        File.WriteAllText(mdPath, mdText, new UTF8Encoding(true));
        Check((await opener.OpenAsync(mdPath)).Success, "Markdown opens separately from the ordinary document.");
        demonstrationFile = mdPath;
        var md = (IMarkdownEditorViewModel)nav.CurrentPage!.DataContext;
        Check(md.MarkdownText == mdText, "Markdown source keeps CRLF and syntax.");
        if (!verify) Shot(main, "S17-a", "md.edit", "Markdown source and save commands", []);
        md.ToggleView.Execute(null); await Settle(main);
        Check(md.IsPreviewMode && md.PreviewDocument is not null && Descendants(md.PreviewDocument).OfType<Table>().Any(), "Markdown preview renders a table from source.");
        Check(Visuals<Image>(Named<RichTextBox>(main, "PreviewEditor")).Any(), "Markdown preview loads the relative local image.");
        if (!verify) Shot(main, "S17-b", "md.preview", "rendered Markdown examples", []);
        md.ToggleView.Execute(null); md.MarkdownText += "\r\nSaved Ж Є ä"; await ((AsyncRelayCommand)md.SaveDocument).ExecuteAsync();
        Check(File.ReadAllBytes(mdPath).SequenceEqual(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(md.MarkdownText))), "Markdown Save preserves BOM, source and CRLF.");
        string markdownBeforeHelp = md.MarkdownText; string? pageBeforeHelp = nav.CurrentKey;
        var parked = ((IWorkspaceDocumentSession)session).CaptureInactiveDocument();
        md.OpenSyntaxHelp.Execute(null); await Settle(main);
        var help = Application.Current.Windows.OfType<HelpWindow>().Single(); var helpVm = (HelpWindowViewModel)help.DataContext;
        Check(helpVm.CurrentTopicId == "md.syntax" && nav.CurrentKey == pageBeforeHelp && md.MarkdownText == markdownBeforeHelp,
            "Markdown help opens md.syntax in a separate window and preserves source and main navigation.");
        var parkedAfter = ((IWorkspaceDocumentSession)session).CaptureInactiveDocument()!;
        Check(parked!.Revision == parkedAfter.Revision && parked.SavedRevision == parkedAfter.SavedRevision && parked.FilePath == parkedAfter.FilePath,
            "Opening Markdown help preserves inactive document path and revisions.");
        if (!verify) Shot(help, "S17-c", "md.syntax", "separate syntax help window", []);
        foreach (string topic in Stage5Topics)
        {
            Check(helpVm.OpenTopic(topic), "Embedded article opens: " + topic);
            var article = c.Resolve<HelpContentService>().Find(culture, topic)!;
            if (verify) foreach (string img in article.Images) Check(c.Resolve<HelpContentService>().LoadImage(culture, article, img) is not null, "Embedded image loads: " + topic + "/" + img);
        }
        help.Close();
        nav.Navigate("Home"); await Settle(main);
        Check(ReferenceEquals(editor.Document, ordinaryDocument) && Text(editor.Document) == ordinaryText && ReferenceEquals(editor.Document.Background, ordinaryBackground),
            "Returning from Markdown help retains the exact ordinary document, its text and background.");
        nav.Navigate("MarkdownEditor"); await Settle(main);
        Check(md.MarkdownText == markdownBeforeHelp, "Returning to Markdown preserves its source.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static IEnumerable<DocumentStructureNode> Flatten(IEnumerable<DocumentStructureNode> roots)
    { foreach (var node in roots) { yield return node; foreach (var child in Flatten(node.Children)) yield return child; } }
    private static Task Respond<T>(Func<T, Task> response) where T : Window
    {
        var completion = new TaskCompletionSource();
        Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            try { await Task.Delay(120); await response(Application.Current.Windows.OfType<T>().Single(w => w.IsVisible)); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); foreach (var window in Application.Current.Windows.OfType<T>().ToArray()) window.Close(); }
        }, DispatcherPriority.ApplicationIdle);
        return completion.Task;
    }
    private static async Task PopupShot<T>(Window main, string id, string topic, bool verify) where T : FrameworkElement
    {
        var control = Visuals<T>(main).First();
        var toggle = Visuals<ToggleButton>(control).First(); toggle.IsChecked = true; await Settle(main);
        var popup = LogicalTreeHelper.GetChildren(Visuals<Grid>(control).First()).OfType<Popup>().Single();
        Check(popup.IsOpen && popup.Child.IsVisible, "Production popup is visible: " + id);
        if (!verify) Shot(main, id, topic, "toolbar popup", [], surface: (FrameworkElement)popup.Child);
        toggle.IsChecked = false; await Settle(main);
    }
}
