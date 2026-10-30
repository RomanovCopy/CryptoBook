using Autofac;
using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Validation;
using CryptoBook.ViewModels;
using CryptoBook.Views;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

internal static partial class Program
{
    private static readonly string[] Stage6Topics = ["files.navigate", "files.views", "files.create", "files.transfer", "files.conflicts", "files.manage", "files.favorites", "files.pinned", "files.recent", "files.batch", "files.properties"];
    private static async Task ValidateStage6(IsolatedServices isolated, bool verify)
    {
        var c = isolated.Container;
        await isolated.PrepareDemoDriveAsync();
        var main = Application.Current.Windows.OfType<MainWindow>().Single();
        c.Resolve<IThemeManager>().ApplyTheme(ApplicationTheme.Light);
        main.WindowState = WindowState.Normal; main.Width = 1440; main.Height = 960;
        var windows = c.Resolve<IWindowManager>();
        var scope = windows.FindHostWindow(((IMainWindowViewModel)main.DataContext).WindowId)!.Scope;
        var nav = scope.Resolve<IPageNavigationService>(); nav.Navigate("Home");
        var session = c.Resolve<IDocumentSession>();
        var open = c.Resolve<IWorkspaceFileOpenService>();
        string source = Path.Combine(isolated.DemoDirectory, "Source"), target = Path.Combine(isolated.DemoDirectory, "Target");
        Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        foreach (string name in new[] { "Unavailable", "MoveTarget", "DropTarget", "CancelTarget" })
            Directory.CreateDirectory(Path.Combine(isolated.DemoDirectory, name));
        Directory.CreateDirectory(Path.Combine(source, "Nested"));
        string alpha = Path.Combine(source, "Alpha.txt"), beta = Path.Combine(source, "Beta.txt");
        File.WriteAllText(alpha, "Disposable Alpha Ж Є ä\n"); File.WriteAllText(beta, "Disposable Beta\n");
        File.WriteAllText(Path.Combine(source, "Nested", "Notes.md"), "# Disposable notes\n");
        demonstrationFile = alpha;
        Check((await open.OpenAsync(alpha)).Success, "Disposable current document opens.");
        c.Resolve<IFileExplorerService>().Show(source); await Settle(main);
        var explorer = Application.Current.Windows.OfType<FileExplorer>().Single(); explorer.Width = 1360; explorer.Height = 860;
        var vm = (IFileExplorerViewModel)explorer.DataContext;
        await Until(() => vm.ChildrenView.OfType<IFileItem>().Count() == 2); await Settle(explorer);
        async Task Go(string path)
        {
            vm.AddressText = path; vm.ApplyAddressCommand.Execute(null);
            await Until(() => SameLocalPath(vm.CurrentPath, path)); await Settle(explorer);
        }
        ISystemItem Item(string name) => vm.ChildrenView.OfType<ISystemItem>().Single(i => Path.GetFileName(i.FullPath) == name);
        void Select(params ISystemItem[] items)
        {
            var list = Visuals<ListView>(explorer).Single(); list.SelectedItems.Clear();
            foreach (var item in items) list.SelectedItems.Add(item);
            vm.SelectedListItem = items.FirstOrDefault(); vm.SelectedItemsSnapshot = items;
            if (items.Length > 0) list.ScrollIntoView(items[0]);
        }
        vm.AddressText = "cancelled-input"; vm.CancelAddressCommand.Execute(null);
        Check(SameLocalPath(vm.AddressText, source), "Escape command restores the current address.");
        if (!verify) Shot(explorer, "S18-a", "files.navigate", "areas 1-3", [
            new(1, new Rect(10, 4, 166, 39), new Point(790, 23)), new(2, Bounds(explorer, Visuals<TextBox>(explorer).Single(b => b.Text == vm.AddressText)), new Point(1310, 65)),
            new(3, Bounds(explorer, Visuals<TreeView>(explorer).Single()), new Point(240, 680))]);
        await Go(target); vm.BackCommand.Execute(null); await Until(() => SameLocalPath(vm.CurrentPath, source));
        vm.ForwardCommand.Execute(null); await Until(() => SameLocalPath(vm.CurrentPath, target));
        vm.CurrentDocumentCommand.Execute(null); await Until(() => SameLocalPath(vm.CurrentPath, source));
        vm.UpCommand.Execute(null); await Until(() => SameLocalPath(vm.CurrentPath, isolated.DemoDirectory)); await Go(source);
        Check(vm.BackCommand.CanExecute(null), "Back/Forward/Up/current-document navigate independently of the workspace.");
        var listView = Visuals<ListView>(explorer).Single();
        var header = Visuals<GridViewColumnHeader>(explorer).First(h => h.Tag?.ToString() == "Name");
        header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Settle(explorer);
        Check(vm.ChildrenView.OfType<IFileItem>().Select(i => i.Name).Distinct().Count() == 2, "Actual list header sorting retains all files.");
        vm.IsFlatViewEnabled = true; await Until(() => !vm.IsFlatViewLoading && vm.ChildrenView.OfType<IFileItem>().Count() == 3);
        vm.FilterText = "Nested"; await Settle(explorer); await Task.Delay(350);
        Check(Path.GetFileName(vm.ChildrenView.OfType<IFileItem>().Single().FullPath) == "Notes.md", "Flat filter matches the relative folder.");
        if (!verify) Shot(explorer, "S18-b", "files.views", "areas 1-3", [new(1, Bounds(explorer, Visuals<ToggleButton>(explorer).Single(b => b.Content?.ToString() == LocalizationManager.GetString("Explorer.FlatView.Toggle"))), new Point(1000, 65)), new(2, Bounds(explorer, Visuals<TextBox>(explorer).Single(b => b.Text == "Nested")), new Point(1230, 65)), new(3, Bounds(explorer, listView))]);
        vm.FilterText = ""; vm.IsFlatViewEnabled = false; await Settle(explorer);
        string gone = Path.Combine(isolated.DemoDirectory, "Unavailable"); Directory.CreateDirectory(gone); await Go(gone);
        Directory.Delete(gone); vm.RefreshCommand.Execute(vm.SelectedItem); await Until(() => vm.IsCurrentDirectoryUnavailable);
        Check(!vm.CreateDirectoryCommand.CanExecute(null), "Unavailable directory disables creation.");
        if (!verify) Shot(explorer, "S18-c", "files.navigate", "unavailable directory and retry", []);
        Directory.CreateDirectory(gone); vm.RetryNavigationCommand.Execute(null); await Until(() => !vm.IsCurrentDirectoryUnavailable);
        await Go(source);

        // Real WPF creation dialogs, including cancellation and invalid/duplicate folder names.
        using (var dialog = OnVisible<DirectoryNameDialog>(async w =>
        {
            w.DirectoryName = Local("Новая папка", "Нова папка", "Neuer Ordner", "New folder"); await Settle(w);
            if (!verify) Shot(w, "S18-d", "files.create", "step 2", [new(2, Bounds(w, (TextBox)w.FindName("DirectoryNameTextBox")), new Point(374, 25))]);
            Buttons(w).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        })) vm.CreateDirectoryCommand.Execute(null);
        string newFolder = Path.Combine(source, Local("Новая папка", "Нова папка", "Neuer Ordner", "New folder"));
        await Until(() => Directory.Exists(newFolder));
        using (var dialog = OnVisible<DirectoryNameDialog>(w => { w.Close(); return Task.CompletedTask; })) vm.CreateDirectoryCommand.Execute(null);
        await Until(() => !Application.Current.Windows.OfType<DirectoryNameDialog>().Any());
        using (var error = OnVisible<MessageWindow>(w => { w.Close(); return Task.CompletedTask; }))
        using (var dialog = OnVisible<DirectoryNameDialog>(w => { w.DirectoryName = "bad|folder"; Buttons(w).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); return Task.CompletedTask; }))
            vm.CreateDirectoryCommand.Execute(null);
        await Until(() => !Application.Current.Windows.OfType<MessageWindow>().Any());
        Check(!Directory.Exists(Path.Combine(source, "bad|folder")), "Invalid directory name creates no directory.");
        bool duplicateFolderError = false;
        using (var error = OnVisible<MessageWindow>(w => { duplicateFolderError = true; w.Close(); return Task.CompletedTask; }))
        using (var dialog = OnVisible<DirectoryNameDialog>(w => { w.DirectoryName = Path.GetFileName(newFolder); Buttons(w).Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); return Task.CompletedTask; }))
        {
            vm.CreateDirectoryCommand.Execute(null);
            await Until(() => duplicateFolderError);
        }
        Check(duplicateFolderError && Directory.Exists(newFolder), "Duplicate directory is rejected without replacing the existing folder.");
        vm.CreateFileCommand.Execute(null); await Settle(explorer);
        var creation = Application.Current.Windows.OfType<NewFileDialog>().Single();
        var createVm = (INewFileDialogViewModel)creation.DataContext;
        isolated.FolderPicker.Paths.Enqueue(source); createVm.Browse.Execute(null);
        await Until(() => !createVm.IsBusy && createVm.TargetDirectory == source);
        createVm.SelectedTemplate = createVm.Templates.Single(t => t is PlainTextTemplate);
        createVm.SelectedNewTemplate.Execute(null); createVm.FileName = "bad|name.txt";
        var filesBeforeInvalidName = Directory.GetFiles(source);
        Check(createVm.Create.CanExecute(null), "Create file is available after validating the disposable destination.");
        await ((AsyncRelayCommand)createVm.Create).ExecuteAsync(); await Settle(main);
        Check(creation.IsVisible && !string.IsNullOrWhiteSpace(createVm.ErrorMessage) && Directory.GetFiles(source).Order().SequenceEqual(filesBeforeInvalidName.Order()), "Q11 fixed: invalid file name leaves the dialog open with an error and creates no substitute file.");
        createVm.FileName = "Created.txt";
        await ((AsyncRelayCommand)createVm.Create).ExecuteAsync(); await Settle(main);
        string actualCreated = session.FilePath!;
        Check(!creation.IsVisible && Path.GetFileName(actualCreated) == "Created.txt", "Correcting the name creates and opens the requested file.");
        WriteJson($"docs/help/screenshots/validation/stage6/creation-{culture}.json", new { requested = "bad|name.txt", rejectedWithoutCreation = true, corrected = Path.GetFileName(actualCreated), existingAlphaUnchanged = File.ReadAllText(alpha) == "Disposable Alpha Ж Є ä\n" });
        await open.OpenAsync(alpha); await Go(source); vm.RefreshCommand.Execute(vm.SelectedItem); await Settle(explorer);

        // In-application file clipboard does not alter the Windows clipboard.
        var previousClipboard = Clipboard.GetDataObject();
        try
        {
            Select(Item("Alpha.txt"), Item("Beta.txt")); await Settle(explorer);
            Check(!vm.RenameClickCommand.CanExecute(vm.SelectedItemsSnapshot) && vm.BatchRenameCommand.CanExecute(vm.SelectedItemsSnapshot), "Multiple local files enable batch and disable single rename.");
            if (!verify) Shot(explorer, "S19-a", "files.transfer", "step 1", [new(1, Bounds(explorer, listView))]);
            vm.CopyCommand.Execute(vm.SelectedItemsSnapshot); await Go(target); vm.PasteCommand.Execute(null);
            await Until(() => File.Exists(Path.Combine(target, "Beta.txt")) && !Application.Current.Windows.OfType<ProgressWindow>().Any());
            Check(File.ReadAllBytes(alpha).SequenceEqual(File.ReadAllBytes(Path.Combine(target, "Alpha.txt"))), "Copy/Paste preserves source and identical destination bytes.");
            await Go(source); Select(Item("Beta.txt")); vm.CutCommand.Execute(vm.SelectedItemsSnapshot);
            string moveTarget = Path.Combine(isolated.DemoDirectory, "MoveTarget"); Directory.CreateDirectory(moveTarget); await Go(moveTarget);
            vm.PasteCommand.Execute(null); await Until(() => File.Exists(Path.Combine(moveTarget, "Beta.txt")) && !File.Exists(beta) && c.Resolve<IFileClipboardService>().GetData().IsEmpty);
            Check(c.Resolve<IFileClipboardService>().GetData().IsEmpty, "Successful Cut/Paste clears the application move intent.");
            await Go(source); Select(Item("Alpha.txt")); vm.CopyPathCommand.Execute(vm.SelectedItemsSnapshot);
            Check(Clipboard.GetText().Contains("Alpha.txt"), "Copy path writes the selected path to the disposable clipboard.");
            var coordinator = c.Resolve<IFileOperationCoordinator>();
            foreach (var action in new[] { FileConflictAction.Skip, FileConflictAction.KeepBoth, FileConflictAction.Cancel, FileConflictAction.Replace })
            {
                File.WriteAllText(Path.Combine(target, "Alpha.txt"), "Old destination");
                using var choose = OnVisible<FileConflictDialog>(async w =>
                {
                    var conflict = (FileConflictDialogViewModel)w.DataContext; conflict.ApplyToAll = true; await Settle(w);
                    if (!verify && action == FileConflictAction.KeepBoth) Shot(w, "S20-a", "files.conflicts", "step 2", []);
                    (action switch { FileConflictAction.Replace => conflict.ReplaceCommand, FileConflictAction.Skip => conflict.SkipCommand, FileConflictAction.KeepBoth => conflict.KeepBothCommand, _ => conflict.CancelCommand }).Execute(null);
                });
                var result = await coordinator.TransferAsync([alpha], target, FileTransferKind.Copy);
                bool replaced = File.ReadAllText(Path.Combine(target, "Alpha.txt")) == File.ReadAllText(alpha);
                Check(action == FileConflictAction.Cancel ? result.Canceled && !replaced : result.Success && replaced == (action == FileConflictAction.Replace), $"Conflict {action} has the expected destination result.");
                Check(File.ReadAllText(alpha) == "Disposable Alpha Ж Є ä\n", $"Conflict {action} preserves copy source.");
                if (action == FileConflictAction.KeepBoth) Check(Directory.GetFiles(target, "Alpha*.txt").Length == 2, "Keep both creates a second free destination name.");
            }
            string errorTarget = Path.Combine(isolated.DemoDirectory, "ReadErrorTarget"); Directory.CreateDirectory(errorTarget);
            using (var locked = new FileStream(alpha, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var failed = await coordinator.TransferAsync([alpha], errorTarget, FileTransferKind.Copy);
                Check(!failed.Success && failed.Failure is not null && File.Exists(alpha), "A real source sharing lock returns a transfer error and preserves the source.");
            }
            Check(File.ReadAllText(alpha) == "Disposable Alpha Ж Є ä\n", "Source bytes remain intact after failed copy.");
            // Apply-to-all must suppress the second real conflict dialog.
            File.WriteAllText(beta, "Disposable Beta\n"); int conflictCount = 0;
            using (var choose = OnVisible<FileConflictDialog>(w => { conflictCount++; var f = (FileConflictDialogViewModel)w.DataContext; f.ApplyToAll = true; f.SkipCommand.Execute(null); return Task.CompletedTask; }))
            {
                var all = await coordinator.TransferAsync([alpha, beta], target, FileTransferKind.Copy);
                Check(all.Success && all.SkippedCount == 2 && conflictCount == 1, "Apply-to-all skips two conflicts with one dialog.");
            }
            bool canceledAtWindow = false;
            using (var cancel = OnVisible<ProgressWindow>(w =>
            {
                w.UpdateLayout();
                if (!verify) Shot(w, "S19-b", "files.transfer", "step 3", []);
                ((IProgressViewModel)w.DataContext).Canceled.Execute(null); canceledAtWindow = true; return Task.CompletedTask;
            }, 1))
            {
                string cancelTarget = Path.Combine(isolated.DemoDirectory, "CancelTarget"); Directory.CreateDirectory(cancelTarget);
                var result = await coordinator.TransferAsync([alpha], cancelTarget, FileTransferKind.Copy);
                Check(canceledAtWindow && result.Canceled && File.Exists(alpha), "Cancel in real progress window stops the transfer and retains the copy source.");
            }
            // Drop command uses the same coordinator; no physical drag gesture is claimed.
            string dropTarget = Path.Combine(isolated.DemoDirectory, "DropTarget"); Directory.CreateDirectory(dropTarget);
            var request = new FileDropRequest([beta], dropTarget, FileTransferKind.Copy);
            Check(vm.DropCommand.CanExecute(request), "Drop accepts a local copy request."); vm.DropCommand.Execute(request);
            await Until(() => File.Exists(Path.Combine(dropTarget, "Beta.txt")) && !Application.Current.Windows.OfType<ProgressWindow>().Any());
            Check(File.ReadAllBytes(beta).SequenceEqual(File.ReadAllBytes(Path.Combine(dropTarget, "Beta.txt"))), "Drop copy preserves bytes.");
            await Go(source); Select(Item("Beta.txt")); vm.MoveCommand.Execute(vm.SelectedItemsSnapshot);
            await Until(() => File.Exists(Path.Combine(isolated.DemoDirectory, "Beta.txt")) && !File.Exists(beta));
            Check(File.Exists(Path.Combine(isolated.DemoDirectory, "Beta.txt")), "Move command uses the isolated folder-picker destination.");

            // Pin and recent links follow single and batch renames of the active document.
            var pinned = scope.Resolve<ISideMenuViewModel>().PinnedDocuments;
            await pinned.InitializeAsync(); await ((AsyncRelayCommand)pinned.ToggleCurrentCommand).ExecuteAsync();
            var recent = c.Resolve<IRecentDocumentService>(); await recent.InitializeAsync();
            await Go(source); vm.RefreshCommand.Execute(vm.SelectedItem); await Settle(explorer);
            Select(Item("Alpha.txt")); var renameItem = vm.SelectedListItem!;
            Check(!vm.BatchRenameCommand.CanExecute(vm.SelectedItemsSnapshot), "One file does not offer batch rename.");
            vm.RenameClickCommand.Execute(vm.SelectedItemsSnapshot); await Until(() => renameItem.IsEditing);
            renameItem.Name = "Cancelled.txt";
            using (var confirm = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
                vm.CancelRenameCommand.Execute(renameItem);
            await Until(() => !renameItem.IsEditing);
            Check(renameItem.Name == "Alpha" && File.Exists(alpha), "Confirmed Escape cancels inline rename without touching disk.");
            vm.RenameClickCommand.Execute(new[] { renameItem }); await Until(() => renameItem.IsEditing); renameItem.Name = "Renamed.txt"; await Settle(explorer);
            if (!verify) Shot(explorer, "S21-a", "files.manage", "step 2", [new(2, Bounds(explorer, listView))]);
            vm.RenameCommand.Execute(renameItem); string renamed = Path.Combine(source, "Renamed.txt");
            await Until(() => File.Exists(renamed) && session.FilePath == renamed);
            Check(pinned.Items.Any(i => i.Path == renamed) && recent.Items.Any(i => i.Path == renamed), "Single rename updates current, pinned and recent paths.");

            // Favorites use their actual view-model commands and a real name dialog.
            vm.Favorites.AddCurrentDirectoryCommand.Execute(vm.CurrentPath); await Until(() => vm.Favorites.Items.Count == 1);
            var favorite = vm.Favorites.Items.Single();
            using (var input = OnVisible<TextInputDialog>(w =>
            {
                ((TextBox)w.FindName("ValueTextBox")).Text = Local("Рабочие файлы", "Робочі файли", "Arbeitsdateien", "Working files");
                Buttons(w).Single(b => b.IsDefault).Command.Execute(null); return Task.CompletedTask;
            })) vm.Favorites.RenameCommand.Execute(favorite);
            await Until(() => vm.Favorites.Items.Single().DisplayName == Local("Рабочие файлы", "Робочі файли", "Arbeitsdateien", "Working files"));
            await Go(target); vm.Favorites.OpenCommand.Execute(vm.Favorites.Items.Single()); await Until(() => SameLocalPath(vm.CurrentPath, source)); await Settle(explorer);
            if (!verify) Shot(explorer, "S22-a", "files.favorites", "step 1", [new(1, Bounds(explorer, Buttons(explorer).Single(b => b.Command == vm.Favorites.AddCurrentDirectoryCommand)), new Point(790, 23))], new Rect(0, 0, 1344, 119));
            vm.Favorites.RemoveCommand.Execute(vm.Favorites.Items.Single()); await Until(() => vm.Favorites.Items.Count == 0);
            Check(Directory.Exists(source), "Removing a favorite leaves its folder intact.");

            string second = Path.Combine(source, "Second.txt"); File.WriteAllText(second, "Disposable second\n");
            vm.RefreshCommand.Execute(vm.SelectedItem); await Until(() => vm.ChildrenView.OfType<IFileItem>().Any(i => Path.GetFileName(i.FullPath) == "Second.txt")); await Settle(explorer);
            Select(Item("Renamed.txt"), Item("Second.txt"));
            var orderedNames = vm.ChildrenView.OfType<IFileItem>().Where(i => vm.SelectedItemsSnapshot.Contains(i)).Select(i => Path.GetFileName(i.FullPath)).ToArray();
            using (var batchWatch = OnVisible<BatchRenameWindow>(async w =>
            {
                w.Width = 1120; w.Height = 800; var batch = (BatchRenameViewModel)w.DataContext;
                await Until(() => batch.Entries.Count == 2);
                Check(batch.Entries.Select(e => e.OldName).SequenceEqual(orderedNames), "Batch order follows the visible list, independent of selection order.");
                batch.NameMask = "same"; await batch.RefreshPreviewAsync(); await Settle(w);
                Check(!batch.CanApply && batch.Entries.All(e => e.Error is not null), "Duplicate preview blocks apply.");
                if (!verify) Shot(w, "S24-b", "files.batch", "conflicting preview", []);
                batch.NameMask = "[N1-3]_[C]"; batch.CounterStart = "7"; batch.CounterStep = "2"; batch.CounterDigits = "3";
                batch.Search = "^(.+)_([0-9]+)"; batch.Replacement = "$2_$1"; batch.UseRegex = true; batch.CaseIndex = (int)RenameCase.Lower;
                await batch.RefreshPreviewAsync(); await Settle(w);
                Check(batch.CanApply && batch.Entries[0].NewName.StartsWith("007_") && batch.Entries[1].NewName.StartsWith("009_"), "Ranges/counter/regex groups/case produce the shown plan.");
                if (!verify) Shot(w, "S24-a", "files.batch", "areas 1-3", [new(1, Bounds(w, Named<TextBox>(w, "NameMaskBox")), new Point(300, 65)), new(2, Bounds(w, Named<DataGrid>(w, "PreviewGrid")), new Point(1090, 450)), new(3, Bounds(w, Named<Button>(w, "ApplyButton")), new Point(980, 682))]);
                var names = batch.Entries.ToArray(); await batch.ApplyCommand.ExecuteAsync(); await Settle(w);
                string currentNew = names.Single(e => e.SourcePath == renamed).TargetPath;
                Check(File.Exists(currentNew) && session.FilePath == currentNew && pinned.Items.Any(i => i.Path == currentNew) && recent.Items.Any(i => i.Path == currentNew), "Batch apply updates actual files, current, pinned and recent paths.");
                if (!verify) Shot(w, "S24-c", "files.batch", "result and undo", [new(1, Bounds(w, Buttons(w).Single(b => b.Command == batch.UndoCommand)), new Point(704, 682))]);
                await batch.UndoCommand.ExecuteAsync();
                Check(File.Exists(renamed) && File.Exists(second) && session.FilePath == renamed && pinned.Items.Any(i => i.Path == renamed) && recent.Items.Any(i => i.Path == renamed), "Undo restores original names and all document links while window remains open.");
                w.Close();
            })) vm.BatchRenameCommand.Execute(vm.SelectedItemsSnapshot);
            await Settle(explorer);
            vm.RefreshCommand.Execute(vm.SelectedItem); await Until(() => vm.ChildrenView.OfType<IFileItem>().Any(i => Path.GetFileName(i.FullPath) == "Second.txt")); await Settle(explorer);
            var extensionItem = Item("Second.txt");
            vm.RenameClickCommand.Execute(new[] { extensionItem }); await Until(() => extensionItem.IsEditing);
            extensionItem.Name = "WithoutExtension"; vm.RenameCommand.Execute(extensionItem);
            string withoutExtension = Path.Combine(source, "WithoutExtension.txt");
            await Until(() => File.Exists(withoutExtension) && !File.Exists(second) && !extensionItem.IsEditing && SameLocalPath(extensionItem.FullPath, withoutExtension));
            Check(File.ReadAllText(withoutExtension) == "Disposable second\n" && extensionItem is IFileItem { Extension: ".txt", Name: "WithoutExtension" }, "Single rename without an explicit extension preserves extension, contents and separate display columns.");
            vm.RenameClickCommand.Execute(new[] { extensionItem }); await Until(() => extensionItem.IsEditing);
            extensionItem.Name = "Second.txt"; vm.RenameCommand.Execute(extensionItem);
            await Until(() => File.Exists(second) && !File.Exists(withoutExtension) && !extensionItem.IsEditing);
            WriteJson($"docs/help/screenshots/validation/stage6/rename-extension-{culture}.json", new { original = "Second.txt", typed = "WithoutExtension", actual = "WithoutExtension.txt", restored = File.Exists(second), bytesPreserved = File.ReadAllText(second) == "Disposable second\n" });

            // Shell metadata is read for a generated PNG with a known format and hash.
            string png = Path.Combine(source, "Sample.png");
            File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg=="));
            vm.RefreshCommand.Execute(vm.SelectedItem); await Until(() => vm.ChildrenView.OfType<IFileItem>().Any(i => Path.GetFileName(i.FullPath) == "Sample.png"));
            Select(Item("Sample.png")); vm.DetailedPropertiesCommand.Execute(vm.SelectedItemsSnapshot); await Settle(explorer);
            var details = Application.Current.Windows.OfType<FileDetailsWindow>().Single(); var detailsVm = (FileDetailsViewModel)details.DataContext;
            await Until(() => !detailsVm.IsBusy && detailsVm.Rows.Count > 0); await Settle(details);
            Check(detailsVm.DetectedType.Contains("PNG"), "Detailed properties detects PNG from disposable bytes.");
            if (!verify) Shot(details, "S25-a", "files.properties", "calculate hashes button", [new(1, Bounds(details, Buttons(details).Single(b => b.Command == detailsVm.HashCommand)), new Point(1030, 119))]);
            await detailsVm.ComputeHashesAsync();
            Check(detailsVm.Rows.Single(r => r.Key == "SHA256").Value == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(png))) && detailsVm.Rows.Single(r => r.Key == "SHA512").Value == Convert.ToHexString(SHA512.HashData(File.ReadAllBytes(png))), "Both hash rows equal independently calculated known file hashes.");
            detailsVm.SearchText = "SHA-"; await Settle(details);
            Check(detailsVm.VisibleRows.OfType<FilePropertyEntry>().Any(r => r.Key == "SHA256") && detailsVm.VisibleRows.OfType<FilePropertyEntry>().Any(r => r.Key == "SHA512") && detailsVm.VisibleRows.Cast<object>().Count() < detailsVm.Rows.Count && detailsVm.CreateReport().Contains("PNG"), "Property filter retains both hashes; full report includes filtered-out rows.");
            if (!verify) Shot(details, "S25-b", "files.properties", "step 3", []);
            Buttons(details).Single(b => b.Content?.ToString() == LocalizationManager.GetString("FileDetails.CopyReport")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(Clipboard.GetText().Contains("SHA512") && Clipboard.GetText().Contains("PNG"), "Copy full report uses all properties despite active filter.");
            details.Close();
            vm.RefreshCommand.Execute(vm.SelectedItem); await Settle(explorer); Select(Item("Sample.png"));
            using (var refuseDelete = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).CancelCommand.Execute(null); return Task.CompletedTask; }))
                vm.DeleteCommand.Execute(vm.SelectedItemsSnapshot);
            await Settle(explorer);
            Check(File.Exists(png), "Declining local deletion leaves the file intact.");
            using (var confirmDelete = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
                vm.DeleteCommand.Execute(vm.SelectedItemsSnapshot);
            await Until(() => !File.Exists(png) && !Application.Current.Windows.OfType<ProgressWindow>().Any());
            Check(Directory.GetDirectories(source, LocalOperationJournal.Prefix + "*").Length > 0, "Confirmed local deletion retains a persistent recovery folder.");
            using (var restoreMessages = OnVisible<MessageWindow>(w => { ((IMessageWindowViewModel)w.DataContext).OkCommand.Execute(null); return Task.CompletedTask; }))
            {
                vm.RestoreFilesCommand.Execute(null);
                await Until(() => File.Exists(png)); await Settle(explorer);
            }
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(png))) == detailsVm.Rows.Single(r => r.Key == "SHA256").Value, "Restore files returns the deleted bytes through the actual explorer command.");
            explorer.Close();
            var model = scope.Resolve<IMainWindowModel>(); model.IsMenuOpen = true; await Settle(main);
            Check(pinned.Items.Single().Path == renamed, "Pinned path remains correct after undo.");
            if (!verify) Shot(main, "S23-a", "files.pinned", "areas 1-2", [], new Rect(0, 32, 740, 780));
            await ((AsyncRelayCommand)pinned.UnpinCommand).ExecuteAsync(pinned.Items.Single()); Check(File.Exists(renamed), "Unpin leaves the file intact.");
            model.IsMenuOpen = false;
            await ((AsyncRelayCommand)scope.Resolve<IMenuFileViewModel>().CloseFile).ExecuteAsync(); nav.Navigate("Home"); await Settle(main);
            var home = (IHomeViewModel)nav.CurrentPage!.DataContext;
            await home.RecentDocuments.InitializeAsync();
            Check(home.RecentDocuments.Items.Any(i => i.Path == renamed), "Start screen recent list shows the renamed path.");
            if (!verify) Shot(main, "S23-b", "files.recent", "step 1", []);
            await ((AsyncRelayCommand)home.RecentDocuments.RemoveCommand).ExecuteAsync(home.RecentDocuments.Items.Single(i => i.Path == renamed));
            Check(File.Exists(renamed) && !home.RecentDocuments.Items.Any(i => i.Path == renamed), "Remove recent entry preserves the physical file.");
        }
        finally
        {
            if (previousClipboard is null) Clipboard.Clear(); else Clipboard.SetDataObject(previousClipboard, false);
        }
        c.Resolve<IHelpWindowService>().Open("files.navigate"); await Settle(main);
        var help = Application.Current.Windows.OfType<HelpWindow>().Single(); var helpVm = (HelpWindowViewModel)help.DataContext;
        if (verify)
        {
            var content = c.Resolve<HelpContentService>();
            foreach (string id in Stage6Topics)
            {
                helpVm.Navigate.Execute(id); await Settle(help);
                var topic = content.Find(culture, id)!;
                Check(helpVm.CurrentTopicId == id && topic is not null && Text(helpVm.Article).Contains(topic.Title), $"Embedded article opens: {id}.");
                foreach (var path in topic!.Images)
                    Check(content.LoadImage(culture, topic, path) is not null, $"Embedded localized image opens: {id}/{path}.");
            }
        }
        help.Close();
        Check(isolated.Paths.All(p => p.StartsWith(isolated.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), "Every persistent store remains inside the disposable validation root.");
    }
    private static bool SameLocalPath(string actual, string expected) => string.Equals(
        Path.GetFullPath(StorageLocation.Parse(actual).OpaqueId).TrimEnd('\\'),
        Path.GetFullPath(expected).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
