using Autofac;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.ViewModels;
using CryptoBook.Views;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

internal static partial class Program
{
    private static readonly string[] Stage8Topics = ["security.password", "security.protect", "security.decrypt", "security.lock", "security.compatibility", "recovery.backup", "recovery.crash", "recovery.lock", "recovery.write"];

    private static async Task ValidateStage8(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.Width = 1440; main.Height = 960; main.WindowState = WindowState.Normal;
        var mainModel = (MainWindowViewModel)main.DataContext;
        var scope = c.Resolve<IWindowManager>().FindHostWindow(mainModel.WindowId)!.Scope;
        var nav = scope.Resolve<IPageNavigationService>();
        nav.Navigate("Home"); await Settle(main);
        Check(main.FindName("CrashRecoveryNotice") is null && main.FindName("LockSnapshotNotice") is null,
            "Startup has no automatic crash- or lock-recovery banners.");
        if (verify) { await VerifyStage8Resources(c, main); return; }
        await isolated.PrepareDemoDriveAsync();
        c.Resolve<IWorkspaceService>().SetWorkspaceDirectory(isolated.DemoDirectory);
        var session = c.Resolve<IDocumentSession>();
        var workspace = (IWorkspaceDocumentSession)session;
        var editor = c.Resolve<IRichTextBoxService>();
        var menu = c.Resolve<IMenuFileViewModel>();
        var key = c.Resolve<IKeyProvider>();
        var secure = c.Resolve<ISecureFileProcessor>();
        var security = c.Resolve<IFileSecurityService>();
        var factory = c.Resolve<ISystemItemCreateService>();
        var demoParent = factory.CreateDirectory(isolated.DemoDirectory, factory.CreateRoot(Path.GetPathRoot(isolated.DemoDirectory)!));
        var open = c.Resolve<IWorkspaceFileOpenService>();
        var reset = c.Resolve<IKeyResetService>();
        reset.UpdateTimeout(TimeSpan.Zero);
        var windows = c.Resolve<IWindowManager>();
        var recovery = (DocumentRecoveryService)c.Resolve<IDocumentRecoveryService>();
        var snapshots = c.Resolve<ILockSnapshotService>();
        string demoKey = "Help-" + Guid.NewGuid().ToString("N") + "!7";
        string body = Local("Вымышленный план поездки.", "Вигаданий план подорожі.", "Fiktiver Reiseplan.", "Fictional journey plan.");
        string title = Local("План поездки", "План подорожі", "Reiseplan", "Journey plan");
        string file = Path.Combine(isolated.DemoDirectory, title + ".cbook");
        demonstrationFile = file;
        void ClearWorkspace() { workspace.RestoreInactiveDocument(null); session.Close(); }
        void SetDemoKey() { char[] chars = demoKey.ToCharArray(); try { key.SetKey(chars); } finally { Array.Clear(chars); } }
        void Click(Window w, string name) => ((Button)w.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        key.Clear();
        using (var cancel = OnVisible<KeyInputWindow>(w => { Click(w, "CancelButton"); return Task.CompletedTask; }))
            Check(!c.Resolve<IEncryptionKeyRequestService>().EnsureEncryptionKeyAvailable() && !key.HasKey, "Cancelling key entry leaves no key.");
        using (var enter = OnVisible<KeyInputWindow>(async w =>
        {
            await Settle(w);
            Shot(w, "S33-a", "security.password", "new key prompt; hidden characters only", []);
            var first = (PasswordBox)w.FindName("PasswordBox");
            var repeat = (PasswordBox)w.FindName("RepeatPasswordBox");
            first.Password = demoKey;
            RaiseSearchKey(first, w, Key.Enter);
            Check(!first.IsEnabled && repeat.IsEnabled, "Enter moves new-key entry to its repeat field.");
            repeat.Password = "Different-demo-value!8";
            Click(w, "OkButton"); await Settle(w);
            Check(w.IsVisible && !string.IsNullOrWhiteSpace(((TextBlock)w.FindName("ErrorText")).Text), "Mismatched repeat is rejected without installing a key.");
            Shot(w, "S33-b", "security.password", "repeat mismatch", []);
            first.Password = demoKey; repeat.Password = demoKey; Click(w, "OkButton");
        }))
            Check(c.Resolve<IEncryptionKeyRequestService>().EnsureEncryptionKeyAvailable() && key.CanEncrypt, "A repeated valid new key is accepted by the production behavior.");
        Check(!EncryptionPasswordPolicy.IsStrong("12345678") && !EncryptionPasswordPolicy.IsStrong("AbcAbcAbc") && EncryptionPasswordPolicy.IsStrong(demoKey), "Weak digit/repetition passwords are rejected for new encryption.");

        // Save in-memory content directly to a protected document. No plaintext staging for capture.
        session.Open(Path.Combine(isolated.DemoDirectory, title + ".XamlPackage"), new XamlPackageFileTemplate(),
            new FlowDocument(new Paragraph(new Bold(new Run(body)))));
        session.MarkDirty();
        isolated.SavePicker.Targets.Enqueue(new DocumentSaveTarget(file, new SecureFileTemplate()));
        using (var confirm = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
            await ((AsyncRelayCommand)menu.SaveAsFile).ExecuteAsync();
        Check(File.Exists(file) && !session.IsDirty && session.Template is SecureFileTemplate, "Save As creates a protected document after its real warning.");
        Check(!File.Exists(Path.ChangeExtension(file, ".XamlPackage")), "Protected Save As did not create a plaintext source copy.");
        byte[] version1 = File.ReadAllBytes(file);
        ClearWorkspace();
        Check((await open.OpenAsync(file)).Success && Text(editor.Document).Contains(body), "Protected document reopens through the production workspace pipeline.");
        var boldRange = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
        Check(boldRange.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight && weight == FontWeights.Bold, "Protected XamlPackage preserves bold formatting.");
        editor.Document.Blocks.Add(new Paragraph(new Run("Version 2"))); session.MarkDirty();
        using (var confirm = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
            await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
        Check(File.ReadAllBytes(file + ".bak").SequenceEqual(version1), "The separate .bak is the previous protected version.");
        byte[] version2 = File.ReadAllBytes(file);
        mainModel.IsMenuOpen = true; await Settle(main);
        var restoreButton = Buttons(main).Single(b => Visuals<TextBlock>(b).Any(t => t.Text == LocalizationManager.GetString("SideMenu.RestoreBackup")));
        Check(restoreButton.IsEnabled, "Restore previous version is available for an existing sidecar.");
        Shot(main, "S37-a", "recovery.backup", "previous-version menu entry", [new(1, Bounds(main, restoreButton))], new Rect(0, 40, 950, 850));
        isolated.DocumentDialogs.BackupChoices.Enqueue(false);
        await ((AsyncRelayCommand)restoreButton.Command).ExecuteAsync();
        Check(Text(editor.Document).Contains("Version 2"), "Declining previous-version recovery preserves the current editor.");
        isolated.DocumentDialogs.BackupChoices.Enqueue(true);
        await ((AsyncRelayCommand)restoreButton.Command).ExecuteAsync();
        mainModel.IsMenuOpen = false; await Settle(main);
        Check(!Text(editor.Document).Contains("Version 2") && session.IsDirty && File.ReadAllBytes(file).SequenceEqual(version2), "Backup recovery loads a dirty previous version without writing over the current file.");
        Shot(main, "S37-b", "recovery.backup", "recovered previous version awaits explicit save", []);
        ClearWorkspace();

        // Actual file-manager protected-copy and mode dialogs; service checks additionally cover copy destinations.
        string plain = Path.Combine(isolated.DemoDirectory, "Source.txt"); File.WriteAllText(plain, body);
        var item = factory.CreateFile(plain, demoParent);
        c.Resolve<IFileExplorerService>().Show(isolated.DemoDirectory); await Settle(main);
        var explorer = Application.Current.Windows.OfType<FileExplorer>().Single(); explorer.Width = 1280; explorer.Height = 850;
        var vm = (IFileExplorerViewModel)explorer.DataContext;
        using (var cancelMode = OnVisible<EncryptionModeWindow>(async w =>
        {
            await Settle(w); Shot(w, "S34-a", "security.protect", "file copy or replacement", []);
            ((IEncryptionMode_ViewModel)w.DataContext).ButtonCancel.Execute(null);
        })) vm.EncryptCommand.Execute(new[] { item });
        await Until(() => !Application.Current.Windows.OfType<EncryptionModeWindow>().Any(w => w.IsVisible));
        Check(File.ReadAllText(plain) == body, "Cancelling encryption mode leaves the source intact.");
        bool summary = false;
        using (var result = OnVisible<MessageWindow>(w => { summary = true; ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
        {
            vm.CreateProtectedCopyCommand.Execute(new[] { item }); await Until(() => summary);
        }
        string copy = Directory.GetFiles(isolated.DemoDirectory, "Source*.cbook").Single();
        await Settle(explorer); Shot(explorer, "S34-b", "security.protect", "verified copy beside the unchanged source", []);
        Check(File.ReadAllText(plain) == body && (await secure.ReadOriginalExtensionAsync(copy)) == ".txt", "Verified protected copy preserves the source and its original extension.");
        var copies = await security.CreateProtectedCopiesAsync([item], isolated.DemoDirectory);
        Check(copies.Success && Directory.GetFiles(isolated.DemoDirectory, "Source*.cbook").Length == 2 && File.Exists(copy), "A protected-copy name collision keeps both existing outputs.");
        string manualCopy = Path.Combine(isolated.DemoDirectory, "Copy.cbook");
        Check((await security.EncryptAsync(item, manualCopy, EncryptionTargetMode.SaveAs)).Success && File.ReadAllText(plain) == body, "File encryption Save As preserves plaintext source bytes.");
        string replacePath = Path.Combine(isolated.DemoDirectory, "Replace.txt");
        File.WriteAllText(replacePath, body);
        var replaceItem = factory.CreateFile(replacePath, demoParent);
        Check((await security.EncryptAsync(replaceItem, replaceItem.FullPath, EncryptionTargetMode.ReplaceSource)).Success, "File encryption replacement completes.");
        Check(File.ReadAllBytes(replaceItem.FullPath).AsSpan().StartsWith("CBKSEC02"u8) && !Encoding.UTF8.GetString(File.ReadAllBytes(replaceItem.FullPath)).Contains(body), "Replacement contains V2 ciphertext even when its old extension remains.");
        await using (var input = await secure.DecryptFileContentAsync(replaceItem.FullPath))
        using (var reader = new StreamReader(input.Content)) Check(await reader.ReadToEndAsync() == body, "Replaced file authenticates and decrypts to original bytes.");

        // The real single-file export UI. Cancel before publishing, then exercise each format separately.
        bool optionsDone = false;
        var decryptItem = factory.CreateFile(file, demoParent);
        Check(vm.DecryptCommand.CanExecute(new[] { decryptItem }), "The production decryption command is enabled for the selected protected file.");
        using var decryptError = OnVisible<MessageWindow>(w =>
        {
            string message = string.Join(" | ", Visuals<TextBlock>(w).Select(t => t.Text));
            w.Close(); throw new InvalidOperationException("Unexpected decryption message: " + message);
        });
        using (var options = OnVisible<DecryptionOptionsWindow>(async w =>
        {
            var model = (DecryptionOptionsViewModel)w.DataContext;
            Check(model.HasConvertibleFormats, "Protected editor content offers original, RTF and plain text exports.");
            await Settle(w); Shot(w, "S35-a", "security.decrypt", "format and destination choices", []);
            model.SelectedOutputFormat = DecryptionOutputFormat.PlainText; model.SelectedTargetMode = EncryptionTargetMode.ReplaceSource;
            await Settle(w); Shot(w, "S35-b", "security.decrypt", "plain-text replacement warning before confirmation", []);
            model.ButtonCancel.Execute(null);
            optionsDone = true;
        }))
        {
            vm.DecryptCommand.Execute(new[] { decryptItem });
            await Until(() => optionsDone);
        }
        decryptError.Dispose();
        Check(File.ReadAllBytes(file).SequenceEqual(version2), "Cancelling decryption options preserves protected source bytes.");
        explorer.Close();
        var export = c.Resolve<IDecryptionExportService>();
        foreach (var format in new[] { DecryptionOutputFormat.Original, DecryptionOutputFormat.Rtf, DecryptionOutputFormat.PlainText })
        {
            await using var prepared = await export.PrepareAsync(file);
            string target = Path.Combine(isolated.DemoDirectory, "Export-" + format + export.GetOutputExtension(prepared.OriginalExtension, format));
            await export.PublishAsync(prepared, new(EncryptionTargetMode.SaveAs, format), target);
            Check(File.Exists(target) && File.ReadAllBytes(file).SequenceEqual(version2), $"{format} export creates a separate result and retains the container.");
            ClearWorkspace(); Check((await open.OpenAsync(target)).Success && Text(editor.Document).Contains(body), $"{format} export reopens with the expected text.");
            var firstParagraph = editor.Document.Blocks.OfType<Paragraph>().First();
            bool exportedBold = new TextRange(firstParagraph.ContentStart, firstParagraph.ContentEnd)
                .GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fontWeight && fontWeight == FontWeights.Bold;
            Check(exportedBold == (format != DecryptionOutputFormat.PlainText), $"{format} export has the documented bold-format retention.");
        }
        ClearWorkspace();
        string replaceContainer = Path.Combine(isolated.DemoDirectory, "Unwrap.cbook"); File.Copy(copy, replaceContainer);
        await using (var prepared = await export.PrepareAsync(replaceContainer))
            await export.PublishAsync(prepared, new(EncryptionTargetMode.ReplaceSource, DecryptionOutputFormat.Original), Path.ChangeExtension(replaceContainer, ".txt"));
        Check(!File.Exists(replaceContainer) && File.ReadAllText(Path.ChangeExtension(replaceContainer, ".txt")) == body, "Decrypt replacement removes the container only after publishing the correct output.");

        // A folder is processed recursively, independently of verified single-file copies.
        string folder = Path.Combine(isolated.DemoDirectory, "Folder"); Directory.CreateDirectory(Path.Combine(folder, "Nested"));
        File.WriteAllText(Path.Combine(folder, "A.txt"), body); File.WriteAllBytes(Path.Combine(folder, "Nested", "B.bin"), [0, 1, 2, 254]);
        string protectedFolder = Path.Combine(isolated.DemoDirectory, "ProtectedFolder");
        Check((await security.EncryptAsync(factory.CreateDirectory(folder, demoParent), protectedFolder, EncryptionTargetMode.SaveAs)).Success, "Recursive folder copy encryption succeeds through its service.");
        Check(File.ReadAllText(Path.Combine(folder, "A.txt")) == body, "Folder-copy mode preserves the original tree.");
        string decryptedFolder = Path.Combine(isolated.DemoDirectory, "DecryptedFolder");
        Check((await security.DecryptAsync(factory.CreateDirectory(protectedFolder, demoParent), decryptedFolder, EncryptionTargetMode.SaveAs)).Success, "Recursive folder decryption copy succeeds.");
        Check(File.ReadAllText(Path.Combine(decryptedFolder, "A.txt")) == body && File.ReadAllBytes(Path.Combine(decryptedFolder, "Nested", "B.bin")).SequenceEqual(new byte[] { 0, 1, 2, 254 }), "Recursive folder decryption restores text and binary bytes.");
        Check((await security.EncryptAsync(factory.CreateDirectory(folder, demoParent), folder, EncryptionTargetMode.ReplaceSource)).Success &&
            File.ReadAllBytes(Path.Combine(folder, "A.txt")).AsSpan().StartsWith("CBKSEC02"u8), "Folder replacement encrypts the original tree in place.");
        Check((await security.DecryptAsync(factory.CreateDirectory(folder, demoParent), folder, EncryptionTargetMode.ReplaceSource)).Success &&
            File.ReadAllText(Path.Combine(folder, "A.txt")) == body, "Folder replacement decryption returns the original filenames and contents.");
        var batchItems = new[] { "Batch-A.txt", "Batch-B.txt" }.Select(name => { string path = Path.Combine(isolated.DemoDirectory, name); File.WriteAllText(path, body); return (ISystemItem)factory.CreateFile(path, demoParent); }).ToArray();
        Check((await security.EncryptAsync(batchItems)).Success, "Batch encryption replaces both disposable sources.");
        Check((await security.DecryptAsync(batchItems, Path.Combine(isolated.DemoDirectory, "BatchOutput"))).Success, "Batch decryption can keep the protected sources in a separate-output mode.");
        bool cancelled = false;
        using (var cancelProgress = OnVisible<ProgressWindow>(w => { ((IProgressViewModel)w.DataContext).Canceled.Execute(null); return Task.CompletedTask; }, 1))
        {
            try { await c.Resolve<IProgressDialogService>().RunAsync(Local("Шифрование", "Шифрування", "Verschlüsselung", "Encryption"), async (progress, token) => { await Task.Delay(200, token); return await security.EncryptAsync(item, Path.Combine(isolated.DemoDirectory, "Canceled.cbook"), EncryptionTargetMode.SaveAs, progress, token); }); }
            catch (OperationCanceledException) { cancelled = true; }
        }
        Check(cancelled && File.ReadAllText(plain) == body && !File.Exists(Path.Combine(isolated.DemoDirectory, "Canceled.cbook")), "Cancellation through the live progress window before writing preserves source and publishes nothing.");

        // Recovery snapshots: write, reject wrong key, restore and defer through the actual service.
        ClearWorkspace(); Check((await open.OpenAsync(file)).Success, "Protected file opens for recovery checks.");
        editor.Document.Blocks.Add(new Paragraph(new Run("Unsaved recovery"))); session.MarkDirty();
        await recovery.SaveSnapshotNowAsync(); await recovery.StopAsync();
        Check(recovery.HasSnapshot, "A password-protected crash snapshot is saved separately from .bak and lock snapshot.");
        string recoveryPath = Path.Combine(isolated.Root, "Recovery", "current.recovery");
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(recoveryPath)).Contains("Unsaved recovery"), "Crash snapshot does not retain demonstration plaintext.");
        key.Clear(); key.SetKey("Wrong-demo-key!3".ToCharArray());
        bool wrongRecovery = false; try { await recovery.RestoreSnapshotAsync(); } catch (CryptographicException) { wrongRecovery = true; }
        Check(wrongRecovery && recovery.HasSnapshot, "Wrong password cannot restore or delete the protected crash snapshot.");
        SetDemoKey(); ClearWorkspace();
        Check(await recovery.RestoreSnapshotAsync() && session.IsDirty && Text(editor.Document).Contains("Unsaved recovery"), "The crash service restores unsaved content under the correct key.");
        await recovery.DeferSnapshotAsync();
        Check(!recovery.HasSnapshot && Directory.GetFiles(Path.GetDirectoryName(recoveryPath)!, "*.deferred").Length == 1, "Deferring preserves a separate crash snapshot.");
        using (var restarted = new DocumentRecoveryService(session, editor, c.Resolve<IFlowDocumentSaveService>(), c.Resolve<IFlowDocumentLoadService>(), c.Resolve<IFileTemplateRegistry>(), main.Dispatcher, recoveryPath, markdownDocument: c.Resolve<IMarkdownDocumentState>(), keyProvider: key))
        {
            Check(restarted.HasSnapshot, "Re-created crash service promotes a deferred snapshot.");
            var coordinator = new DocumentCloseCoordinator(session, c.Resolve<ICurrentDocumentSaver>(), restarted, isolated.DocumentDialogs);
            await coordinator.InitializeAsync();
            Check(!restarted.HasSnapshot && Directory.GetFiles(Path.GetDirectoryName(recoveryPath)!, "*.deferred").Length == 1, "Current startup coordinator defers the old snapshot without showing a recovery prompt.");
            await restarted.StopAsync();
        }
        ClearWorkspace();

        var crashVm = scope.Resolve<CrashRecoveryViewModel>();
        mainModel.IsMenuOpen = true; await Settle(main);
        var crashButton = Buttons(main).Single(b => b.Command == RecoveryCommands.RestoreCrashSnapshot);
        Check(crashVm.CanRestore && RecoveryCommands.RestoreCrashSnapshot.CanExecute(null, crashButton), "Deferred crash recovery is available only through the explicit File menu command.");
        crashButton.BringIntoView(); await Settle(main);
        Shot(main, "S37-d", "recovery.crash", "manual File menu recovery command", [new(1, Bounds(main, crashButton))]);
        mainModel.IsMenuOpen = false;
        Check((await open.OpenAsync(plain)).Success, "Current ordinary work can coexist with deferred crash recovery.");
        editor.Document.Blocks.Add(new Paragraph(new Run("Keep current editor"))); session.MarkDirty();
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Cancel);
        await ((AsyncRelayCommand)crashVm.Restore).ExecuteAsync();
        Check(session.IsDirty && Text(editor.Document).Contains("Keep current editor") && recovery.DeferredSnapshotTime is not null, "Cancelling the current-work decision prevents crash recovery and preserves both versions.");
        ClearWorkspace(); key.Clear();
        using (var cancelKey = OnVisible<KeyInputWindow>(w => { Click(w, "CancelButton"); return Task.CompletedTask; }))
            await ((AsyncRelayCommand)crashVm.Restore).ExecuteAsync();
        Check(recovery.DeferredSnapshotTime is not null && !session.HasDocument && !key.HasKey, "Cancelling crash-recovery key entry preserves the deferred copy.");
        key.Clear(); key.SetKey("Wrong-demo-key!5".ToCharArray());
        using (var error = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
            await ((AsyncRelayCommand)crashVm.Restore).ExecuteAsync();
        Check(crashVm.CanRestore && crashVm.Error.Length > 0 && recovery.DeferredSnapshotTime is not null && !session.HasDocument, "A wrong crash-recovery key leaves the deferred copy and current workspace unchanged.");
        SetDemoKey(); RecoveryCommands.RestoreCrashSnapshot.Execute(null, main);
        await Until(() => !crashVm.CanRestore && session.IsDirty); await Settle(main);
        Check(!crashVm.CanRestore && session.IsDirty && Text(editor.Document).Contains("Unsaved recovery") && recovery.HasSnapshot, "The explicit menu command restores the deferred snapshot and persists a new crash copy before consuming it.");
        Shot(main, "S37-e", "recovery.crash", "restored unsaved work awaits explicit save", []);
        ClearWorkspace();

        // Ordinary documents only lose the cached key. They are not converted into protected documents by Ctrl+L.
        Check((await open.OpenAsync(plain)).Success, "Ordinary file opens before key-only reset.");
        editor.Document.Blocks.Add(new Paragraph(new Run("Ordinary unsaved"))); session.MarkDirty();
        Check(await reset.ResetAsync() && !key.HasKey && session.HasDocument && session.IsDirty && !snapshots.Exists, "Reset with only an ordinary document clears the cached key and preserves unsaved ordinary work without a lock snapshot.");
        SetDemoKey(); ClearWorkspace();
        Check((await open.OpenAsync(file)).Success, "Protected ordinary editor opens before workspace locking.");
        editor.Document.Blocks.Add(new Paragraph(new Run("Protected ordinary unsaved"))); session.MarkDirty();
        byte[] mdBytes = Encoding.UTF8.GetBytes("# Protected Markdown\n\nUnsaved Markdown.");
        await using (var mdInput = new MemoryStream(mdBytes))
            await secure.EncryptStreamAsync(mdInput, ".md", Path.Combine(isolated.DemoDirectory, "ProtectedMarkdown.cbook"));
        isolated.DocumentDialogs.Choices.Enqueue(UnsavedChangesChoice.Discard);
        Check((await open.OpenAsync(Path.Combine(isolated.DemoDirectory, "ProtectedMarkdown.cbook"))).Success, "Protected Markdown opens alongside the independent ordinary editor.");
        c.Resolve<IMarkdownDocumentState>().Text += "\nMarkdown dirty.";
        nav.Navigate("Home"); await Settle(main);
        Check(workspace.CaptureInactiveDocument() is { IsMarkdown: true }, "The dirty protected Markdown document is parked independently.");

        c.Resolve<ISettingsWindowService>().Open(); await Settle(main);
        var settingsWindow = Application.Current.Windows.OfType<SettingsWindow>().Single(); settingsWindow.Width = 1120; settingsWindow.Height = 820;
        var settingsModel = (ISettingsViewModel)settingsWindow.DataContext;
        var timeoutBox = Visuals<ComboBox>(settingsWindow).FirstOrDefault(b => b.ItemsSource is IReadOnlyList<KeyResetIntervalOption>);
        for (int index = 0; index < 8 && (timeoutBox is null || !timeoutBox.IsVisible); index++)
        { settingsModel.SelectedSectionIndex = index; await Settle(settingsWindow); timeoutBox = Visuals<ComboBox>(settingsWindow).FirstOrDefault(b => b.ItemsSource is IReadOnlyList<KeyResetIntervalOption> && b.IsVisible); }
        Check(timeoutBox is not null && timeoutBox.IsVisible, "Security settings expose the actual key-reset intervals.");
        settingsModel.SelectedKeyResetInterval = settingsModel.KeyResetIntervals.Single(i => i.Minutes == 5);
        Check(reset.Timeout == TimeSpan.FromMinutes(5), "Changing the interval takes effect without restarting.");
        settingsModel.SelectedCultureName = culture == "de-DE" ? "en-US" : "de-DE";
        Check(settingsModel.SelectedKeyResetInterval.Minutes == 5 && settingsModel.SelectedKeyResetInterval.DisplayName == LocalizationManager.GetString("Security.ResetInterval.5"), "Live language switching translates the existing selected interval without clearing it.");
        settingsModel.SelectedCultureName = culture;
        await Settle(settingsWindow);
        timeoutBox!.IsDropDownOpen = true; await Settle(settingsWindow);
        Shot(settingsWindow, "S36-a", "security.lock", "security settings and inactivity interval", []);
        timeoutBox.IsDropDownOpen = false; settingsWindow.Close();
        reset.UpdateTimeout(TimeSpan.Zero);
        await ((AsyncRelayCommand)mainModel.ResetEncryptionKey).ExecuteAsync(); await Settle(main);
        Check(!key.HasKey && snapshots.Exists && !session.HasDocument, "Manual reset creates and verifies a lock snapshot before clearing both protected editors and the key.");
        mainModel.IsMenuOpen = true; await Settle(main);
        var lockButton = Buttons(main).Single(b => b.Command == RecoveryCommands.RestoreLockSnapshot);
        Check(RecoveryCommands.RestoreLockSnapshot.CanExecute(null, lockButton), "A retained lock snapshot is available through the File menu without an automatic notice.");
        lockButton.BringIntoView(); await Settle(main);
        Shot(main, "S36-b", "recovery.lock", "manual lock-snapshot menu command", [new(1, Bounds(main, lockButton))]);
        Check(!Encoding.UTF8.GetString(File.ReadAllBytes(snapshots.SnapshotPath)).Contains("Protected ordinary unsaved"), "Lock snapshot has no demonstration plaintext.");
        using (var unlock = OnVisible<UnlockWindow>(async w =>
        {
            ((PasswordBox)w.FindName("KeyBox")).Password = "Wrong-demo-key!4"; Click(w, "OpenButton");
            await Until(() => ((Button)w.FindName("OpenButton")).IsEnabled); await Settle(w);
            Check(!key.HasKey && snapshots.Exists && !string.IsNullOrWhiteSpace(((TextBlock)w.FindName("ErrorText")).Text), "Real unlock dialog rejects a wrong key and retains the snapshot.");
            Shot(w, "S36-c", "recovery.lock", "wrong key and retry", []);
            await Task.Delay(600); ((PasswordBox)w.FindName("KeyBox")).Password = demoKey; Click(w, "OpenButton");
        }))
        using (var choice = OnVisible<LockRecoveryChoiceWindow>(async w =>
        {
            await Settle(w); Shot(w, "S36-d", "recovery.lock", "open saved file or restore unsaved snapshot", []);
            Buttons(w).Single(b => b.Content?.ToString() == LocalizationManager.GetString("Security.RestoreSnapshot")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }))
        {
            RecoveryCommands.RestoreLockSnapshot.Execute(null, lockButton);
            await Until(() => key.HasKey && session.HasDocument && !snapshots.Exists);
        }
        mainModel.IsMenuOpen = false;
        Check(Text(editor.Document).Contains("Protected ordinary unsaved") && workspace.CaptureInactiveDocument() is { IsMarkdown: true } inactive && Encoding.UTF8.GetString(inactive.Content).Contains("Markdown dirty."), "Restoring the lock snapshot restores both protected documents including their unsaved edits.");
        await Settle(main);
        Check(nav.CurrentKey == workspace.ActivePageKey && Visuals<Page>(main).Any(p => p.IsVisible && ReferenceEquals(p, nav.CurrentPage)), "Restoration displays the active recovered editor in the real WPF Frame.");
        Check(Visuals<System.Windows.Controls.RichTextBox>(main).Any(box => box.IsVisible && Text(box.Document).Contains("Protected ordinary unsaved")), "The recovered ordinary document is visible, not merely retained in memory behind an empty Markdown editor.");
        Shot(main, "S36-e", "security.lock", "restored protected workspace", []);

        // Reopening a saved original is a different choice from restoring its unsaved snapshot.
        Check(await reset.ResetAsync() && await reset.TryUnlockAsync(demoKey.AsMemory()), "A second lock snapshot accepts its original key.");
        await reset.RestoreSnapshotAsync(restoreAsUnsaved: false);
        Check(Text(editor.Document).Contains("Version 2") && !Text(editor.Document).Contains("Protected ordinary unsaved"), "Open original returns the saved active document, not its unsaved edits.");
        editor.Document.Blocks.Add(new Paragraph(new Run("Missing original snapshot"))); session.MarkDirty();
        Check(await reset.ResetAsync(), "A snapshot can be retained before the source disappears.");
        string held = file + ".held"; File.Move(file, held);
        try
        {
            var missingChoice = new LockRecoveryChoiceWindow(title, file, originalAvailable: false) { Owner = main };
            missingChoice.Show(); await Settle(missingChoice);
            Check(Buttons(missingChoice).All(b => b.Content?.ToString() != LocalizationManager.GetString("Common.Open")), "Unavailable original offers snapshot recovery without a misleading Open button.");
            missingChoice.Close();
            Check(await reset.TryUnlockAsync(demoKey.AsMemory()), "The retained snapshot authenticates without the original file.");
            await reset.RestoreSnapshotAsync(restoreAsUnsaved: false);
            Check(Text(editor.Document).Contains("Missing original snapshot") && session.IsDirty, "Missing-source recovery falls back to unsaved snapshot contents.");
        }
        finally { File.Move(held, file); }

        // Real sharing failure leaves disk bytes intact and the editor dirty; a later retry succeeds.
        workspace.RestoreInactiveDocument(null);
        ClearWorkspace(); key.Clear(); Check((await open.OpenAsync(plain)).Success, "Ordinary file opens for an independent write-failure check.");
        editor.Document.Blocks.Add(new Paragraph(new Run("Unsaved write retry"))); session.MarkDirty();
        byte[] beforeFailure = File.ReadAllBytes(plain);
        bool errorSeen = false;
        using (var locked = new FileStream(plain, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var error = OnVisible<MessageWindow>(async w =>
        {
            if (!Visuals<TextBlock>(w).Any(t => t.Text == LocalizationManager.GetString("Document.FileInUse")))
            { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return; }
            errorSeen = true; await Settle(w); Shot(w, "S37-c", "recovery.write", "sharing violation; unsaved editor retained", []);
            ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null);
        }, 10)) await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
        Check(errorSeen && File.ReadAllBytes(plain).SequenceEqual(beforeFailure) && session.IsDirty, "Sharing failure retains original file bytes and unsaved editor.");
        await ((AsyncRelayCommand)menu.SaveFile).ExecuteAsync();
        Check(!session.IsDirty && !File.ReadAllBytes(plain).SequenceEqual(beforeFailure), "Saving succeeds after releasing the external sharing lock.");
        Check(isolated.SavePicker.Targets.Count == 0 && isolated.DocumentDialogs.BackupChoices.Count == 0, "All queued save and backup decisions were consumed.");
        WriteJson(Path.GetRelativePath(repo, Path.Combine(isolated.Root, "stage8.json")), new { culture, protectedSave = true, protectedCopy = true, fileCopyAndReplacement = true, recursiveFolder = true, batch = true, formats = new[] { "Original", "RTF", "TXT" }, cancellation = "Live progress-window startup before writing", backup = "Previous version loaded dirty without replacing disk file", crash = "Service restore, deferred snapshot re-creation, explicit previous-session recovery command, wrong-key retention and durable crash-copy replacement", locking = "Both protected editors roundtrip; an ordinary-only document remains editable after key reset", secretsRetained = false });
        ClearWorkspace(); key.Clear();
    }

    private static async Task VerifyStage8Resources(ILifetimeScope c, MainWindow main)
    {
        c.Resolve<IHelpWindowService>().Open(); await Settle(main);
        var help = Application.Current.Windows.OfType<HelpWindow>().Single();
        var vm = (HelpWindowViewModel)help.DataContext;
        var content = c.Resolve<HelpContentService>();
        foreach (string id in Stage8Topics)
        {
            Check(vm.OpenTopic(id), $"Embedded stage8 topic opens: {id}."); await Settle(help);
            var topic = content.Find(culture, id)!;
            Check(topic.Images.Any() && topic.Images.All(path => content.LoadImage(culture, topic, path) is not null), $"Embedded stage8 images load: {id}.");
        }
        help.Close();
    }
}
