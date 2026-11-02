using Autofac;
using CryptoBook.AI;
using CryptoBook.DTO;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Validation;
using CryptoBook.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static partial class Program
{
    private static readonly string[] Stage92Topics = ["ai.chat", "ai.templates", "ai.attachments", "ai.apply"];

    private static async Task ValidateStage92(IsolatedServices isolated, bool verify, bool templatesOnly)
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
            foreach (string id in Stage92Topics)
            {
                Check(vm.OpenTopic(id), $"Embedded stage9.2 topic opens: {id}."); await Settle(help);
                var article = content.Find(culture, id)!;
                Check(article.Images.Any() && article.Images.All(path => content.LoadImage(culture, article, path) is not null), $"Embedded stage9.2 images load: {id}.");
            }
            help.Close(); return;
        }
        string modelPath = Environment.GetEnvironmentVariable("CRYPTOBOOK_HELP_MODEL") ?? throw new InvalidOperationException("Set CRYPTOBOOK_HELP_MODEL.");
        var model = new AiModelSettings { ModelPath = modelPath, MaxTokens = 256, Temperature = 0 };
        model.Validate(); _ = AiVisionProjector.Find(modelPath);
        var store = c.Resolve<AiSettingsStore>();
        var ai = c.Resolve<AiAssistantViewModel>();
        var engine = c.Resolve<IAiInferenceService>();
        var editor = c.Resolve<IRichTextBoxService>();
        var session = c.Resolve<IDocumentSession>();
        var open = c.Resolve<IWorkspaceFileOpenService>();
        var scope = c.Resolve<IWindowManager>().FindHostWindow(((IMainWindowViewModel)main.DataContext).WindowId)!.Scope;
        var nav = scope.Resolve<IPageNavigationService>();
        string ordinary = Path.Combine(isolated.DemoDirectory, "Practice.txt");
        File.WriteAllText(ordinary, Local("Вымышленный план поездки.", "Вигаданий план подорожі.", "Fiktiver Reiseplan.", "Fictional journey plan."));
        byte[] originalBytes = File.ReadAllBytes(ordinary);
        demonstrationFile = ordinary;
        Check((await open.OpenAsync(ordinary)).Success, "Disposable ordinary document opens.");
        store.Save(store.Current with { Enabled = true, SelectedModelPath = modelPath, Models = [model] });
        await Settle(main); ai.IsOpen = true; await Settle(main);
        var panel = Named<CryptoBook.MyControls.AiAssistantPanel>(main, "AiPanel");
        Named<Thumb>(main, "ResizeHandle").RaiseEvent(new DragDeltaEventArgs(-160, 0) { RoutedEvent = Thumb.DragDeltaEvent }); await Settle(main);
        void PanelShot(string id, string topic, string state, Mark[]? marks = null) => Shot(main, id, topic, state, marks ?? [], Bounds(main, panel));
        Mark Above(int number, FrameworkElement element)
        {
            Rect bounds=Bounds(main, element);
            return new(number, bounds, new Point(bounds.X+bounds.Width/2, bounds.Y-20));
        }
        void Click(Window w, string name)
        {
            var button=(Button)w.FindName(name);
            if(button.Command is { } command) { if(command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter); }
            else button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
        AiModelSettingsWindow Settings()
        {
            ai.OpenSettings.Execute(null);
            var window=Application.Current.Windows.OfType<AiModelSettingsWindow>().Single();
            window.Height=1000;
            return window;
        }
        string templateName = Local("Учебный маршрут", "Навчальний маршрут", "Testreise", "Practice journey");
        string instruction = Local("Отвечай по-русски, кратко и точно. Запрошенную таблицу выводи без блока кода.", "Відповідай українською, стисло й точно. Запитану таблицю виводь без блоку коду.", "Antworte kurz und genau auf Deutsch. Gib die gewünschte Tabelle ohne Codeblock aus.", "Reply briefly and accurately in English. Output the requested table without code fences.");
        var settings = Settings(); await Settle(settings);
        var draft = (AiModelSettingsViewModel)settings.DataContext;
        Check(draft.Templates.Items.Count == 6, "Fresh isolated settings contain six bundled conversation templates.");
        Click(settings, "AddPromptTemplateButton");
        ((TextBox)settings.FindName("PromptTemplateNameBox")).Text = "";
        Click(settings, "SaveModelButton"); await Settle(settings);
        Check(settings.IsVisible && draft.Status == LocalizationManager.GetString("AI.TemplateNameRequired") && store.Current.PromptTemplates.Count == 6, "Empty template name is rejected without changing saved templates.");
        ((TextBox)settings.FindName("PromptTemplateNameBox")).Text = draft.Templates.Items[0].Name;
        Click(settings, "SaveModelButton"); await Settle(settings);
        Check(draft.Status == LocalizationManager.GetString("AI.TemplateNameDuplicate"), "Duplicate localized template name is rejected.");
        Shot(settings, "S41-b", "ai.templates", "duplicate template name rejected", []);
        ((TextBox)settings.FindName("PromptTemplateNameBox")).Text = templateName;
        ((TextBox)settings.FindName("SystemPromptBox")).Text = "";
        Click(settings, "SaveModelButton"); await Settle(main);
        string customId = store.Current.SelectedPromptTemplateId;
        Check(!settings.IsVisible && store.Current.PromptTemplates.Count == 7 && store.Current.SelectedPromptTemplate!.SystemPrompt == "", "Creating a unique template with an empty system prompt is supported.");
        settings = Settings(); await Settle(settings); draft = (AiModelSettingsViewModel)settings.DataContext;
        ((TextBox)settings.FindName("SystemPromptBox")).Text = instruction;
        await Settle(settings); Shot(settings, "S41-a", "ai.templates", "steps 1-4: create/edit name and instruction, Save", []);
        Click(settings, "SaveModelButton"); await Settle(main);
        Check(store.Current.SelectedPromptTemplateId == customId && store.Current.SelectedPromptTemplate!.SystemPrompt == instruction, "Editing a saved template preserves its identity and saves the new instruction.");
        string stored = File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json"));
        settings = Settings(); await Settle(settings); draft = (AiModelSettingsViewModel)settings.DataContext;
        draft.Templates.SelectedTemplate!.Name = templateName + Local(" (черновик)", " (чернетка)", " (Entwurf)", " (draft)");
        draft.Templates.Add.Execute(null); draft.Templates.Remove.Execute(null);
        while (draft.Templates.Items.Count > 1) draft.Templates.Remove.Execute(null);
        Check(!draft.Templates.Remove.CanExecute(null), "Last remaining draft template cannot be deleted.");
        await Settle(settings); Shot(settings, "S41-d", "ai.templates", "last draft template, deletion disabled before Cancel", []); settings.Close();
        Check(File.ReadAllText(Path.Combine(isolated.Root, "ai-settings.json")) == stored && store.Current.PromptTemplates.Count == 7, "Cancel discards template edits, creation, deletion and selection together.");
        if (templatesOnly) return;

        string north = Local("Север", "Північ", "Nord", "North"), south = Local("Юг", "Південь", "Süd", "South");
        string expectedTable = Local("| Маршрут | Минуты |", "| Маршрут | Хвилини |", "| Route | Minuten |", "| Route | Minutes |") + $"\n|---|---|\n| {north} | 73 |\n| {south} | 24 |";
        ai.Prompt = Local("Выведи ровно следующую Markdown-таблицу, без блока кода и пояснений:\n", "Виведи саме таку Markdown-таблицю, без блоку коду й пояснень:\n", "Gib genau diese Markdown-Tabelle ohne Codeblock und Erläuterungen aus:\n", "Output exactly the following Markdown table, without code fences or commentary:\n") + expectedTable;
        var key = Named<TextBox>(main, "PromptBox").InputBindings.OfType<KeyBinding>().Single(b => b.Key == Key.Enter && b.Modifiers == ModifierKeys.Control);
        Check(ReferenceEquals(key.Command, ai.Send), "Real prompt control binds Ctrl+Enter to the production Send command.");
        await ai.Send.ExecuteAsync(); await Settle(main);
        var tableMessage = ai.Messages.Last(); string answer = tableMessage.Content;
        Check(engine.IsLoaded && answer.Contains("73") && new CryptoBook.Services.MarkdownFlowDocumentRenderer().Render(answer, null).Blocks.OfType<Table>().Any(), "Actual GGUF output is a Markdown table containing the requested values.");
        Check(ai.SelectedMessageCount == 1 && tableMessage.IsSelected, "Latest real answer is selected automatically.");
        PanelShot("S40-a", "ai.chat", "real table reply, prompt and automatic answer selection");
        var question = ai.Messages[0]; question.IsSelected = true;
        string multiple = $"**{question.RoleLabel}:**\n\n{question.Content}\n\n---\n\n**{tableMessage.RoleLabel}:**\n\n{answer}";
        ai.Copy.Execute(null);
        Check(isolated.TextClipboard.Text == multiple, "Copy preserves original Markdown, participant labels, separators and conversation order in the memory clipboard sink.");
        ai.ClearMessageSelection.Execute(null);
        Check(ai.SelectedMessageCount == 0 && !ai.Copy.CanExecute(null) && !ai.Insert.CanExecute(null) && !ai.Replace.CanExecute(null), "Clear selection disables Copy, Insert and Replace.");
        ai.SelectAllMessages.Execute(null); await Settle(main);
        Check(ai.SelectedMessageCount == 2, "Select all checks both question and answer.");
        PanelShot("S40-b", "ai.chat", "selected messages and transfer toolbar", [Above(1, Named<Button>(main, "SelectAllMessagesButton")), Above(2, Named<Button>(main, "ClearMessageSelectionButton")), Above(3, Named<Button>(main, "CopyMessagesButton"))]);
        isolated.TextClipboard.Fail = true; ai.Copy.Execute(null);
        Check(ai.Status == LocalizationManager.GetString("AI.CopyFailed"), "A failed clipboard sink is reported without changing messages."); isolated.TextClipboard.Fail = false;
        ai.Prompt = Local($"Какое число указано в строке {north}? Ответь только числом.", $"Яке число вказано в рядку {north}? Відповідай лише числом.", $"Welche Zahl steht in der Zeile {north}? Antworte nur mit der Zahl.", $"Which number is in the {north} row? Reply only with the number.");
        await ai.Send.ExecuteAsync(); await Settle(main);
        Check(ai.Messages.Count == 4 && ai.Messages.Last().Content.Contains("73"), "Actual follow-up receives the existing conversation and recalls its table value.");
        Check(question.IsSelected && tableMessage.IsSelected && !ai.Messages.Last().IsSelected, "Manual message selection survives a subsequent generated answer.");
        string followup = ai.Messages.Last().Content;
        ai.Prompt = Local("Черновик следующего вопроса.", "Чернетка наступного запитання.", "Entwurf der nächsten Frage.", "Draft of the next question.");
        ai.Attachments.Add(AiAttachmentService.Document("Draft.txt", "Fictional attachment."));
        ai.SelectedPromptTemplateId = "concise"; await Settle(main);
        Check(engine.IsLoaded && ai.Messages.Count == 4 && ai.Prompt.Length > 0 && ai.Attachments.Count == 1, "Panel template switching preserves loaded weights, chat, draft and attachments.");
        ai.SelectedPromptTemplateId = customId; await Settle(main);
        Check(new AiSettingsStore(Path.Combine(isolated.Root, "ai-settings.json")).Current.SelectedPromptTemplateId == customId, "Selected shared template persists in the isolated settings store.");
        PanelShot("S41-c", "ai.templates", "shared template selected without resetting the existing conversation");
        ai.RemoveAttachment.Execute(ai.Attachments[0]); ai.Prompt = "";
        ai.ClearMessageSelection.Execute(null); tableMessage.IsSelected = true;
        ai.Copy.Execute(null); Check(isolated.TextClipboard.Text == answer, "Single selected reply copies its unmodified source without participant labels.");

        Run ResetOrdinary()
        {
            editor.IsReadOnly = false; editor.Service.IsUndoEnabled = false;
            editor.Document.Blocks.Clear(); var run = new Run("LEFT TARGET RIGHT"); editor.Document.Blocks.Add(new Paragraph(run));
            session.MarkDirty(); editor.Service.IsUndoEnabled = true;
            var start = run.ContentStart.GetPositionAtOffset(5)!; editor.Selection.Select(start, start.GetPositionAtOffset(6)!);
            return run;
        }
        ResetOrdinary(); await Settle(main); string before = Text(editor.Document);
        Check(ai.Insert.CanExecute(null) && ai.Replace.CanExecute(null), "Ordinary editable selection enables both transfer commands.");
        ai.Insert.Execute(null); await Settle(main); string inserted = Text(editor.Document);
        Check(inserted.Contains("LEFT TARGET") && inserted.Contains(" RIGHT") && inserted.Contains("73") && editor.Document.Blocks.OfType<Table>().Any(), "Insert adds a real editable WPF table after the current selection, preserving surrounding text.");
        Shot(main, "S43-a", "ai.apply", "step 3: actual AI table inserted into ordinary editor", []);
        editor.Undo(); Check(Text(editor.Document) == before, "One ordinary Undo removes the complete formatted insertion.");
        editor.Redo(); Check(Text(editor.Document) == inserted, "One ordinary Redo restores the formatted insertion.");
        var table = editor.Document.Blocks.OfType<Table>().Single();
        var cell = table.RowGroups[0].Rows[1].Cells[0]; var cellText = new TextRange(cell.ContentStart, cell.ContentEnd); cellText.Text = north + " edited";
        Check(Text(editor.Document).Contains("edited") && tableMessage.Content == answer, "Inserted table cells are independently editable and do not rewrite the source AI reply.");
        ResetOrdinary(); await Settle(main); before = Text(editor.Document); ai.Replace.Execute(null); await Settle(main);
        string replaced = Text(editor.Document);
        Check(!replaced.Contains("TARGET") && replaced.Contains("LEFT ") && replaced.Contains(" RIGHT") && replaced.Contains("73"), "Replace changes only the currently selected ordinary text and preserves both sides.");
        Shot(main, "S43-b", "ai.apply", "step 4: selection replaced with the real generated table", []);
        editor.Undo(); Check(Text(editor.Document) == before, "One ordinary Undo reverses the complete replacement.");
        editor.Redo(); Check(Text(editor.Document) == replaced, "Ordinary replacement Redo restores its contents.");
        editor.IsReadOnly = true; await Settle(main);
        Check(!ai.Insert.CanExecute(null) && !ai.Replace.CanExecute(null), "Read-only ordinary document blocks AI transfer."); editor.IsReadOnly = false;
        ResetOrdinary(); editor.Selection.Select(editor.Document.ContentEnd, editor.Document.ContentEnd);
        question.IsSelected = true; before = Text(editor.Document); ai.Insert.Execute(null);
        Check(Text(editor.Document).Contains(question.RoleLabel + ":") && Text(editor.Document).Contains(tableMessage.RoleLabel + ":"), "Multi-message ordinary insertion includes ordered participant labels.");
        editor.Undo(); Check(Text(editor.Document) == before, "One Undo reverses the whole multi-message insertion.");
        Check(File.ReadAllBytes(ordinary).SequenceEqual(originalBytes), "AI transfers modify the editor without silently saving the source file.");

        string mdPath = Path.Combine(isolated.DemoDirectory, "Practice.md");
        File.WriteAllText(mdPath, "# Markdown\n\nLEFT TARGET RIGHT\n");
        Check((await open.OpenAsync(mdPath)).Success, "Opening Markdown after the answer arrives selects a different current transfer target.");
        await Settle(main);
        var md = (IMarkdownEditorViewModel)nav.CurrentPage!.DataContext;
        if (!md.IsPreviewMode) md.ToggleView.Execute(null); await Settle(main);
        Check(!ai.Insert.CanExecute(null) && !ai.Replace.CanExecute(null) && ai.InsertUnavailableReason == LocalizationManager.GetString("AI.ApplyNeedsEditor"), "Markdown preview blocks transfer and explains that an editable document is required.");
        Shot(main, "S43-c", "ai.apply", "preview mode, transfer disabled", []);
        md.ToggleView.Execute(null); await Settle(main);
        var sourceBox = (TextBox)nav.CurrentPage.FindName("SourceEditor");
        sourceBox.IsUndoEnabled = false; sourceBox.IsUndoEnabled = true;
        sourceBox.Select(sourceBox.Text.IndexOf("TARGET", StringComparison.Ordinal), 6);
        ai.ClearMessageSelection.Execute(null); tableMessage.IsSelected = true; await Settle(main);
        string mdBefore = sourceBox.Text;
        ai.Insert.Execute(null); await Settle(main); string mdInserted = sourceBox.Text;
        Check(mdInserted == mdBefore.Insert(mdBefore.IndexOf("TARGET", StringComparison.Ordinal) + 6, answer), "Markdown Insert writes exact source Markdown after the current selection.");
        Shot(main, "S43-d", "ai.apply", "source mode, original Markdown from the selected reply inserted", []);
        sourceBox.Undo(); Check(sourceBox.Text == mdBefore && md.MarkdownText == mdBefore, "One Markdown Undo reverses the complete insertion and updates document state.");
        sourceBox.Redo(); Check(sourceBox.Text == mdInserted, "One Markdown Redo restores exact inserted source."); sourceBox.Undo();
        sourceBox.Select(sourceBox.Text.IndexOf("TARGET", StringComparison.Ordinal), 6); ai.Replace.Execute(null); await Settle(main);
        string mdReplaced = sourceBox.Text;
        Check(mdReplaced == mdBefore.Replace("TARGET", answer, StringComparison.Ordinal), "Markdown Replace changes only the selected source text.");
        sourceBox.Undo(); Check(sourceBox.Text == mdBefore, "One Markdown Undo reverses replacement.");
        sourceBox.Redo(); Check(sourceBox.Text == mdReplaced, "Markdown replacement Redo restores exact contents."); sourceBox.Undo();
        sourceBox.Select(sourceBox.Text.Length, 0); question.IsSelected = true; ai.Copy.Execute(null);
        string copied = isolated.TextClipboard.Text!; ai.Insert.Execute(null); await Settle(main);
        Check(sourceBox.Text == mdBefore + copied, "Markdown multi-message insertion equals the original ordered Copy payload.");
        sourceBox.Undo(); Check(sourceBox.Text == mdBefore, "One Undo removes the entire Markdown multi-message insertion.");
        ai.AttachDocument.Execute(null);
        var markdownSnapshot=ai.Attachments.Single();
        Check(markdownSnapshot.Text==mdBefore, "Current document captures the whole Markdown source, including markup, after switching editors.");
        sourceBox.Text += "\nCHANGED AFTER SNAPSHOT"; await Settle(main);
        Check(markdownSnapshot.Text==mdBefore, "Markdown attachment snapshot does not follow later source edits.");
        ai.RemoveAttachment.Execute(markdownSnapshot);
        nav.Navigate("Home"); await Settle(main);
        Check(Text(editor.Document).Contains("LEFT TARGET RIGHT"), "Returning from Markdown retains the independent ordinary editor.");

        await ai.ClearChat.ExecuteAsync();
        string txt = Path.Combine(isolated.DemoDirectory, "Notes.txt"), binary = Path.Combine(isolated.DemoDirectory, "Sample.bin"), imagePath = Path.Combine(isolated.DemoDirectory, "Shapes.png");
        File.WriteAllText(txt, "Fictional route code: MOON-73."); File.WriteAllBytes(binary, Enumerable.Range(0, 400).Select(i => (byte)i).ToArray());
        var drawing = new DrawingVisual(); using(var dc = drawing.RenderOpen())
        { dc.DrawRectangle(Brushes.White, null, new Rect(0,0,256,256)); dc.DrawRectangle(Brushes.Red, null, new Rect(20,20,100,100)); dc.DrawEllipse(Brushes.Blue, null, new Point(180,180),48,48); }
        var image = new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32); image.Render(drawing); WriteBitmap(image,imagePath);
        var paths = Enumerable.Range(0,5).Select(i => Path.Combine(isolated.DemoDirectory, $"Note-{i}.txt")).ToArray();
        foreach(string path in paths) File.WriteAllText(path, "Fictional note.");
        await ai.AttachPaths.ExecuteAsync(paths); await Settle(main);
        Check(ai.Attachments.Count == 4 && !ai.CanAttach && ai.Status == LocalizationManager.GetString("AI.AttachmentLimit"), "A five-file batch retains four accepted cards and reports the attachment limit.");
        PanelShot("S42-c", "ai.attachments", "fifth file rejected, four accepted cards retained");
        foreach(var attachment in ai.Attachments.ToArray()) ai.RemoveAttachment.Execute(attachment);
        isolated.AiPicker.Selections.Enqueue([txt, binary]); await ai.AttachFiles.ExecuteAsync(); await Settle(main);
        Check(ai.Attachments.Count == 2 && ai.Attachments[0].Text.Contains("MOON-73") && ai.Attachments[1].IsBinary && ai.Attachments[1].Text.Contains("000000F0:") && !ai.Attachments[1].Text.Contains("00000100:"), "Queued native-picker result uses the production text/binary readers; binary preview is limited to 256 bytes.");
        PanelShot("S42-a", "ai.attachments", "text and binary cards with limited HEX semantics", [Above(1, Named<Button>(main, "AttachFilesButton")), Above(2, Named<Button>(main, "AttachDocumentButton"))]);
        ai.RemoveAttachment.Execute(ai.Attachments[1]);
        var data = new DataObject(DataFormats.FileDrop, new[] { binary });
        // WPF exposes the routed event, but its event-args constructor is internal.
        var drop = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, [data, DragDropKeyStates.None, DragDropEffects.Copy, panel, new Point(10,10)], null)!;
        drop.RoutedEvent = UIElement.PreviewDropEvent;
        panel.RaiseEvent(drop); await AiUntil(() => ai.Attachments.Count == 2 && ai.CanAttach, "Routed file drop");
        Check(drop.Handled && drop.Effects == DragDropEffects.Copy && ai.Attachments[1].IsBinary, "Actual file-drop behavior routes disposable paths to the same production attachment command.");
        foreach(var attachment in ai.Attachments.ToArray()) ai.RemoveAttachment.Execute(attachment);
        editor.Document.Blocks.Clear(); var whole = new Run("MOON-73\nWhole fictional document.\nEND-OF-DOCUMENT"); editor.Document.Blocks.Add(new Paragraph(whole)); session.MarkDirty();
        editor.Selection.Select(whole.ContentStart.GetPositionAtOffset(8)!, whole.ContentStart.GetPositionAtOffset(13)!);
        ai.AttachDocument.Execute(null);
        var snapshot = ai.Attachments.Single();
        Check(snapshot.Text.Contains("MOON-73") && snapshot.Text.Contains("END-OF-DOCUMENT"), "Current document attaches the whole ordinary document, not only its selection.");
        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text = "CHANGED-94";
        Check(snapshot.Text.Contains("MOON-73") && !snapshot.Text.Contains("CHANGED-94"), "Attached document is an immutable snapshot after subsequent editor changes.");
        isolated.AiPicker.Selections.Enqueue([imagePath]); await ai.AttachFiles.ExecuteAsync(); await Settle(main);
        PanelShot("S42-b", "ai.attachments", "whole-document snapshot and image pending before Send");
        ai.Prompt = Local("Одним предложением назови цвета двух фигур на картинке и код MOON из приложенного документа.", "Одним реченням назви кольори двох фігур на зображенні та код MOON із вкладеного документа.", "Nenne in einem Satz die Farben der beiden Formen im Bild und den MOON-Code aus dem angehängten Dokument.", "In one sentence, name the colors of both shapes in the image and the MOON code from the attached document.");
        await ai.Send.ExecuteAsync(); await Settle(main);
        string vision = ai.Messages.Last().Content;
        Check(new[] { "red", "красн", "червон", "rot" }.Any(s => vision.Contains(s,StringComparison.OrdinalIgnoreCase)) && new[] { "blue", "син", "blau" }.Any(s => vision.Contains(s,StringComparison.OrdinalIgnoreCase)) && vision.Contains("73"), "Actual vision request identifies both image colors and the original document snapshot code.");
        Check(ai.Attachments.Count == 0 && ai.Messages[0].Attachments.Count == 2, "Sent cards move into conversation history and remain available to follow-up questions.");
        PanelShot("S42-d", "ai.attachments", "real mixed vision and document-snapshot answer");
        ai.Prompt = Local("Повтори только код из документа.", "Повтори лише код із документа.", "Wiederhole nur den Code aus dem Dokument.", "Repeat only the code from the document."); await ai.Send.ExecuteAsync();
        Check(ai.Messages.Last().Content.Contains("73") && ai.Messages[0].Attachments.Count == 2, "Actual follow-up retains the document/image attachments from the previous turn.");
        string tooLarge = Path.Combine(isolated.DemoDirectory, "TooLarge.bin"); using(var f=File.Create(tooLarge)) f.SetLength(AiAttachmentService.MaxFileBytes+1L);
        await ai.AttachPaths.ExecuteAsync(new[]{tooLarge});
        Check(ai.Attachments.Count == 0 && ai.Status == LocalizationManager.GetString("AI.AttachmentTooLarge"), "Oversized file is rejected without adding a card.");
        string empty=Path.Combine(isolated.DemoDirectory,"Empty.txt"); File.WriteAllText(empty,""); await ai.AttachPaths.ExecuteAsync(new[]{empty});
        Check(ai.Attachments.Count == 0 && ai.Status == LocalizationManager.GetString("AI.AttachmentEmpty"), "Empty text file produces its localized attachment error.");
        ai.Prompt = "Draft"; await ai.AttachPaths.ExecuteAsync(new[]{txt}); await ai.ClearChat.ExecuteAsync(); await Settle(main);
        Check(ai.Messages.Count == 0 && ai.Attachments.Count == 0 && ai.Prompt.Length == 0 && engine.IsLoaded && ai.SelectedMessageCount == 0, "New chat clears history, draft, attachments and selection while retaining loaded weights.");
        PanelShot("S40-c", "ai.chat", "new chat after clearing previous fictional conversation");
        settings = Settings(); await Settle(settings); draft=(AiModelSettingsViewModel)settings.DataContext;
        draft.Templates.SelectedTemplate = draft.Templates.Items.Single(t=>t.Id==customId); draft.Templates.Remove.Execute(null); Click(settings,"SaveModelButton"); await Settle(main);
        Check(!store.Current.PromptTemplates.Any(t=>t.Id==customId) && store.Current.PromptTemplates.Count==6 && engine.IsLoaded, "Saving template deletion removes only the isolated custom template and retains model weights.");
        Check(isolated.AiPicker.Selections.Count==0,"All queued attachment-picker choices consumed.");
        await ai.Unload.ExecuteAsync();
        WriteJson(Path.GetRelativePath(repo,Path.Combine(isolated.Root,"stage9-chat.json")),new {culture,model=Path.GetFileName(modelPath),answer,followup,vision,templates="Create/edit/empty/duplicate/last/Cancel/select/delete",clipboard="Memory sink only, original source/order and failure checked",editors="Ordinary formatted table, Markdown source, insert/replace/multiple, one-step Undo/Redo, preview/read-only refusal",attachments="Text/limited HEX, queued picker, routed Drop, entire-document immutable snapshot, real mixed vision, follow-up, limits",personalData=false});
    }
}
