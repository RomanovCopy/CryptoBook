using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.Security;
using CryptoBook.FileTemplates;
using CryptoBook.Validation;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Xunit;

namespace CryptoBook.Tests;

public sealed class ArchiveExplorerWindowTests
{
    [WpfFact]
    public async Task VisibleExplorer_BrowsesPreviewsCopiesAndNavigates_AndPromptsForPassword()
    {
        MemorySettingsProvider.Install();
        using var directory = new ArchiveTestDirectory();
        string folder = Directory.CreateDirectory(Path.Combine(directory.Path, "Документы")).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "Текст.txt"), "Просмотр внутри архива 🔐");
        await File.WriteAllTextAsync(Path.Combine(folder, "Другой.txt"), "other");
        string archive = Path.Combine(directory.Path, "Документы.zip");
        var service = directory.CreateService();
        await service.CreateAsync([folder], archive, ArchiveFormat.Zip);
        string protectedArchive = Path.Combine(directory.Path, "Защищённый.7z");
        await service.CreateAsync([folder], protectedArchive, ArchiveFormat.SevenZip, "Пароль 🔐");
        var app = Application.Current ?? new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Resources["Res"] = new ResourceWrapper();
        app.Resources["CurrentMargin"] = new Thickness(10, 2, 10, 2);
        foreach(string resource in new[] { "WindowStyles", "ControlStyles", "ImageStyles", "ListStyles", "ButtonStyles",
            "EditorToolbarIcons", "TreeViewStyles", "ComboBoxStyles", "ThemedControls" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/CryptoBook;component/Styles/{resource}.xaml", UriKind.Relative)
            });
        var messages = new RecordingMessages();
        using var container = new Startup().ConfigureServices(app, builder =>
        {
            builder.RegisterInstance<IMessageService>(messages);
            builder.RegisterInstance<IDocumentRecoveryService>(new NoRecovery());
            builder.RegisterInstance(service).AsSelf().As<IArchiveService>();
            builder.RegisterInstance<IFavoriteDirectoryStore>(new JsonFavoriteDirectoryStore(Path.Combine(directory.Path, "favorites.json")));
            builder.RegisterInstance<IPinnedDocumentStore>(new JsonPinnedDocumentStore(Path.Combine(directory.Path, "pinned.json")));
            builder.RegisterInstance<IRecentDocumentStore>(new JsonRecentDocumentStore(Path.Combine(directory.Path, "recent.json")));
        });
        var windows = container.Resolve<IWindowManager>();
        var listener = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        Guid id = windows.CreateWindow<FileExplorer>(new Dictionary<string, object?>
        {
            [FileExplorerService.InitialDirectoryContextKey] = directory.Path
        });
        var window = Assert.IsType<FileExplorer>(windows.FindHostWindow(id)!.Window);
        var vm = Assert.IsType<FileExplorerViewModel>(window.DataContext);
        var scope = DiScope.GetScope(window)!;
        var model = scope.Resolve<IFileExplorerModel>();
        var light = new ResourceDictionary { Source = new Uri("/CryptoBook;component/Themes/LightTheme.xaml", UriKind.Relative) };
        app.Resources.MergedDictionaries.Add(light);
        try
        {
            window.Width = 1120;
            window.Height = 720;
            window.Show();
            await UntilAsync(() => vm.CurrentPath == directory.Path);
            int driveCount = vm.GetDrives.Count;
            var sourceParent = Assert.IsAssignableFrom<IContainerSystemItem>(vm.SelectedItem);
            var localArchive = sourceParent.Children
                .OfType<IFileItem>().Single(file => file.FullPath == archive);
            Assert.True(vm.OpenArchiveCommand.CanExecute(new[] { localArchive }));
            // Exercise the default double-click path, not only the explicit service.
            vm.ListViewItemDoubleClickCommand.Execute(localArchive);
            await UntilAsync(() => vm.IsArchiveView);
            string root = vm.CurrentPath;
            Assert.Equal(driveCount, vm.GetDrives.Count);
            var archiveDirectory = Assert.IsAssignableFrom<IDirectoryItem>(vm.SelectedItem);
            Assert.Same(sourceParent, archiveDirectory.Parent);
            Assert.Equal("Документы.zip", archiveDirectory.Name);
            Assert.Same(archiveDirectory, Assert.Single(vm.ArchiveDirectories));
            Assert.DoesNotContain(sourceParent.Children, item => ReferenceEquals(item, archiveDirectory));
            Assert.Single(sourceParent.Children.OfType<IFileItem>(), file => file.FullPath == archive);
            var tree = Assert.IsType<TreeView>(window.FindName("treeView"));
            var archiveTreeItem = await FindTreeItemAsync(tree, archiveDirectory);
            Assert.Same(sourceParent, Assert.IsType<TreeViewItem>(ItemsControl.ItemsControlFromItemContainer(archiveTreeItem)).DataContext);
            var archiveLabel = Descendants<TextBlock>(archiveTreeItem).First(text => text.Text == archiveDirectory.Name);
            var labelOrigin = archiveLabel.TransformToAncestor(tree).Transform(new Point(0, 0));
            Assert.InRange(labelOrigin.X, 0, tree.ActualWidth - 20);
            using(var otherScope = container.BeginLifetimeScope(builder =>
                builder.RegisterInstance<IWindowContext>(new WindowContext(new Dictionary<string, object?>()))))
            {
                var other = otherScope.Resolve<IFileExplorerViewModel>();
                try
                {
                    Assert.Empty(other.ArchiveDirectories);
                    Assert.Same(vm.GetDrives, other.GetDrives);
                }
                finally { other.Closed.Execute(null); }
            }
            Assert.EndsWith("Документы.zip!/", vm.CurrentDisplayPath);
            Assert.False(vm.CanUseFlatView);
            var inner = Assert.IsAssignableFrom<IContainerSystemItem>(vm.SelectedItem).Children.OfType<IContainerSystemItem>().Single();
            vm.ListViewItemDoubleClickCommand.Execute(inner);
            await UntilAsync(() => vm.CurrentPath == inner.FullPath);
            var selectedTreeItem = Assert.Single(Descendants<TreeViewItem>(tree), node => ReferenceEquals(node.DataContext, inner));
            var selectedLabel = Descendants<TextBlock>(selectedTreeItem).First(text => text.Text == inner.Name);
            var selectedOrigin = selectedLabel.TransformToAncestor(tree).Transform(new Point(0, 0));
            Assert.InRange(selectedOrigin.X, 0, tree.ActualWidth - 20);
            Assert.InRange(selectedOrigin.Y, 0, tree.ActualHeight - 20);
            var file = inner.Children.OfType<IFileItem>().Single(file => file.Name == "Текст");
            vm.SelectedListItem = file;
            vm.SelectedItemsSnapshot = [file];
            await vm.Preview.SelectAsync(file);
            Assert.Equal(FilePreviewKind.Text, vm.Preview.PreviewKind);
            Assert.Equal("Просмотр внутри архива 🔐", vm.Preview.Text);
            Assert.True(vm.CopyCommand.CanExecute(new[] { file }));
            foreach(var command in new[] { vm.DeleteCommand, vm.CutCommand, vm.RenameClickCommand })
                Assert.True(command.CanExecute(new[] { file }));
            foreach(var command in new[] { vm.OpenWithCommand, vm.EncryptCommand, vm.CreateArchiveCommand })
                Assert.False(command.CanExecute(new[] { file }));
            Assert.False(vm.PasteCommand.CanExecute(null));
            Assert.True(vm.CreateDirectoryCommand.CanExecute(null));
            Assert.False(vm.Favorites.AddCurrentDirectoryCommand.CanExecute(vm.CurrentPath));
            var renameFile = inner.Children.OfType<IFileItem>().Single(item => item.Name == "Другой");
            vm.RenameClickCommand.Execute(new[] { renameFile });
            Assert.True(renameFile.IsEditing);
            renameFile.Name = "Переименованный";
            vm.RenameCommand.Execute(renameFile);
            await UntilAsync(() => !renameFile.IsEditing && !app.Windows.OfType<ProgressWindow>().Any());
            Assert.EndsWith("Переименованный.txt", Uri.UnescapeDataString(renameFile.FullPath));
            var directoryTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) =>
            {
                var dialog = app.Windows.OfType<DirectoryNameDialog>().FirstOrDefault();
                if(dialog is null) return;
                ((TextBox)dialog.FindName("DirectoryNameTextBox")).Text = "Новая папка";
                Descendants<Button>(dialog).Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }, Dispatcher.CurrentDispatcher);
            try { vm.CreateDirectoryCommand.Execute(null); }
            finally { directoryTimer.Stop(); }
            await UntilAsync(() => inner.Children.OfType<IContainerSystemItem>().Any(item => item.Name == "Новая папка") &&
                !app.Windows.OfType<ProgressWindow>().Any());
            // Archive selection removes descendants without interpreting encoded names as Windows paths.
            Assert.Single(FileExplorerSelectionPolicy.NormalizeForOperation(new ISystemItem[] { inner, file }));
            foreach(string culture in new[] { "ru-RU", "en-US", "uk-UA", "de-DE" })
            foreach(string theme in new[] { "Light", "Dark" })
            {
                LocalizationManager.SelectCulture(culture);
                var dictionary = new ResourceDictionary { Source = new Uri($"/CryptoBook;component/Themes/{theme}Theme.xaml", UriKind.Relative) };
                app.Resources.MergedDictionaries.Add(dictionary);
                window.UpdateLayout();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Assert.Contains(ArchivePathPolicy.Text("Editable"), window.Title);
                int confirmationCount = messages.Confirmations.Count;
                vm.DeleteCommand.Execute(new[] { renameFile });
                await UntilAsync(() => messages.Confirmations.Count == confirmationCount + 1);
                var confirmation = messages.Confirmations[^1];
                Assert.Equal(ArchivePathPolicy.Text("DeleteTitle"), confirmation.Title);
                Assert.Equal(ArchivePathPolicy.Text("DeleteWarning"), confirmation.Message);
                Assert.DoesNotContain("Android", confirmation.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(renameFile, inner.Children); // Canceling must preserve the entry.
                Render(window, culture + "-" + theme + "-Explorer");
                Guid promptId = windows.CreateWindow<ArchivePasswordWindow>(new Dictionary<string, object?>
                {
                    ["source"] = protectedArchive, ["retry"] = false
                });
                var prompt = Assert.IsType<ArchivePasswordWindow>(windows.FindHostWindow(promptId)!.Window);
                prompt.Show();
                prompt.UpdateLayout();
                var passwordVm = Assert.IsType<ArchivePasswordViewModel>(prompt.DataContext);
                var passwordBox = Assert.IsType<PasswordBox>(prompt.FindName("ArchivePassword"));
                passwordBox.Password = "temporary";
                Assert.Equal(9, passwordVm.Password!.Length);
                Render(prompt, culture + "-" + theme + "-Password");
                prompt.Close();
                using var ignored = windows.GetResult<System.Security.SecureString>(promptId);
                Assert.Null(passwordVm.Password);
                app.Resources.MergedDictionaries.Remove(dictionary);
            }
            LocalizationManager.SelectCulture("ru-RU");
            messages.AllowConfirmation = true;
            try
            {
                vm.DeleteCommand.Execute(new[] { renameFile });
                await UntilAsync(() => !inner.Children.Contains(renameFile) && !app.Windows.OfType<ProgressWindow>().Any());
            }
            finally { messages.AllowConfirmation = false; }
            Assert.Equal(ArchivePathPolicy.Text("DeleteWarning"), messages.Confirmations[^1].Message);
            Console.WriteLine("ARCHIVE_UI: themes and preview passed");
            // Navigation via Up, Back, Forward, and the editable address uses the same history.
            vm.UpCommand.Execute(null);
            await UntilAsync(() => vm.CurrentPath == root);
            vm.BackCommand.Execute(null);
            await UntilAsync(() => vm.CurrentPath == inner.FullPath);
            Console.WriteLine("ARCHIVE_UI: history and address passed");
            vm.ForwardCommand.Execute(null);
            await UntilAsync(() => vm.CurrentPath == root);
            vm.AddressText = archive + "!/Документы";
            vm.ApplyAddressCommand.Execute(null);
            await UntilAsync(() => vm.CurrentPath == inner.FullPath);
            vm.AddressText = archive + "!/../outside";
            vm.ApplyAddressCommand.Execute(null);
            await UntilAsync(() => messages.Errors.Count == 1);
            Assert.Contains(ArchivePathPolicy.Text("UnsafePath"), messages.Errors[0]);
            messages.Errors.Clear();
            Assert.True(await model.NavigateAsync(inner.FullPath, FileExplorerNavigationMode.Standard));
            vm.FilterText = "Текст";
            await UntilAsync(() => vm.ChildrenView.Cast<object>().Count() == 1);
            vm.FilterText = "";
            vm.CopyCommand.Execute(new[] { file });
            string destination = Directory.CreateDirectory(Path.Combine(directory.Path, "Извлечено")).FullName;
            Assert.True(await model.NavigateAsync(destination, FileExplorerNavigationMode.Standard));
            Assert.True(vm.PasteCommand.CanExecute(null));
            vm.PasteCommand.Execute(null);
            await UntilAsync(() => File.Exists(Path.Combine(destination, "Текст.txt")));
            Assert.Equal("Просмотр внутри архива 🔐", await File.ReadAllTextAsync(Path.Combine(destination, "Текст.txt")));
            await UntilAsync(() => !app.Windows.OfType<ProgressWindow>().Any());
            bool conflictShown = false;
            var conflictTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) =>
            {
                var conflict = app.Windows.OfType<FileConflictDialog>().FirstOrDefault();
                if(conflict is null) return;
                var conflictVm = (FileConflictDialogViewModel)conflict.DataContext;
                Assert.False(conflictVm.ReplaceCommand.CanExecute(null));
                conflictShown = true;
                conflictVm.KeepBothCommand.Execute(null);
            }, Dispatcher.CurrentDispatcher);
            try
            {
                vm.PasteCommand.Execute(null);
                await UntilAsync(() => Directory.GetFiles(destination, "*.txt").Length == 2 && !app.Windows.OfType<ProgressWindow>().Any());
            }
            finally { conflictTimer.Stop(); }
            Assert.True(conflictShown);
            Console.WriteLine("ARCHIVE_UI: copy and paste passed");
            // Refresh retains the nested archive folder and history without adding a drive.
            await model.RefreshDrivesAsync();
            Assert.True(await model.NavigateAsync(root, FileExplorerNavigationMode.Standard));
            Assert.False(vm.IsCurrentDirectoryUnavailable);
            Assert.Same(archiveDirectory, vm.SelectedItem);
            vm.UpCommand.Execute(null);
            await UntilAsync(() => vm.CurrentPath == directory.Path);
            Assert.False(vm.IsArchiveView);
            Assert.Equal(driveCount, vm.GetDrives.Count);
            Assert.DoesNotContain(vm.GetDrives, drive => ArchiveStorageProvider.IsArchive(drive.Location));
            Assert.Single(vm.ChildrenView.OfType<IFileItem>(), file => file.FullPath == archive);
            Assert.DoesNotContain(vm.ChildrenView.Cast<ISystemItem>(), item => ReferenceEquals(item, archiveDirectory));
            vm.BackCommand.Execute(null);
            await UntilAsync(() => vm.CurrentPath == root);
            Assert.Same(archiveDirectory, vm.SelectedItem);
            vm.ForwardCommand.Execute(null);
            await UntilAsync(() => vm.CurrentPath == directory.Path);
            Console.WriteLine("ARCHIVE_UI: drive refresh and exit passed");
            int attempts = 0;
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) =>
            {
                var prompt = app.Windows.OfType<ArchivePasswordWindow>().FirstOrDefault();
                if(prompt is null) return;
                var passwordVm = (ArchivePasswordViewModel)prompt.DataContext;
                var box = (PasswordBox)prompt.FindName("ArchivePassword");
                box.Password = ++attempts == 1 ? "wrong" : "Пароль 🔐";
                passwordVm.AcceptCommand.Execute(null);
            }, Dispatcher.CurrentDispatcher);
            try { Assert.True(await model.OpenArchiveAsync(protectedArchive)); }
            finally { timer.Stop(); }
            Assert.Equal(2, attempts);
            Console.WriteLine("ARCHIVE_UI: password retry passed");
            Assert.True(vm.IsArchiveView);
            Assert.Equal(driveCount, vm.GetDrives.Count);
            Assert.IsAssignableFrom<IDirectoryItem>(vm.SelectedItem);
            Assert.Equal(directory.Path, CryptoBook.Properties.Settings.Default.LastDirectory_FileExplorer);
            // Open an archive document through the real command, close its explorer,
            // edit it, and save through the ordinary File menu into the encrypted archive.
            var protectedFolder = Assert.IsAssignableFrom<IContainerSystemItem>(vm.SelectedItem).Children.OfType<IContainerSystemItem>().Single();
            Assert.True(await model.NavigateAsync(protectedFolder.FullPath, FileExplorerNavigationMode.Standard));
            var protectedText = Assert.IsAssignableFrom<IContainerSystemItem>(vm.SelectedItem).Children.OfType<IFileItem>().Single(item => item.Name == "Текст");
            vm.OpenCommand.Execute(new[] { protectedText });
            var documents = container.Resolve<IDocumentSession>();
            await UntilAsync(() => documents.FilePath?.StartsWith("archive://", StringComparison.Ordinal) == true);
            window.Close();
            var editor = container.Resolve<IRichTextBoxService>();
            editor.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Сохранено обратно")));
            Assert.True(documents.IsDirty);
            Assert.True(await container.Resolve<IMenuFileModel>().TrySaveCurrentAsync());
            Assert.False(documents.IsDirty);
            documents.Close();
            string savedOutput = Path.Combine(directory.Path, "Сохранённый архив");
            await service.ExtractAsync(protectedArchive, savedOutput, "Пароль 🔐");
            Assert.Contains("Сохранено обратно", await File.ReadAllTextAsync(Path.Combine(savedOutput, "Документы/Текст.txt")));
            // CryptoBook protection is preserved independently of the archive password.
            container.Resolve<IKeyProvider>().SetKey("g7L!2x9R#v4M$z8Q");
            var secureFiles = container.Resolve<ISecureFileProcessor>();
            string protectedDocument = Path.Combine(directory.Path, "Защищённый.cbook");
            using(var plaintext = new MemoryStream("Защищённый документ"u8.ToArray()))
                await secureFiles.EncryptStreamAsync(plaintext, ".txt", protectedDocument);
            string documentArchive = Path.Combine(directory.Path, "protected-document.zip");
            await service.CreateAsync([protectedDocument], documentArchive, ArchiveFormat.Zip);
            var archives = container.Resolve<ArchiveStorageProvider>();
            Guid documentOwner = Guid.NewGuid();
            var documentRoot = await archives.MountAsync(documentOwner, documentArchive, null, default);
            var protectedEntry = Assert.Single(await archives.GetChildrenAsync(documentRoot));
            Assert.True((await container.Resolve<IWorkspaceFileOpenService>().OpenAsync(protectedEntry.Location.ToString())).Success);
            archives.CloseOwner(documentOwner);
            Assert.IsType<SecureFileTemplate>(documents.Template);
            editor.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Защита сохранена")));
            messages.AllowConfirmation = true;
            try { Assert.True(await container.Resolve<IMenuFileModel>().TrySaveCurrentAsync()); }
            finally { messages.AllowConfirmation = false; }
            documents.Close();
            string protectedOutput = Path.Combine(directory.Path, "protected-output");
            await service.ExtractAsync(documentArchive, protectedOutput);
            string savedProtectedFile = Path.Combine(protectedOutput, "Защищённый.cbook");
            Assert.True(await container.Resolve<ISecureFileValidator>().HasCryptoBookHeaderAsync(savedProtectedFile));
            await using(var decrypted = await secureFiles.DecryptFileContentAsync(savedProtectedFile))
            {
                var restored = await container.Resolve<IFlowDocumentLoadService>().PrepareAsync(decrypted.Content, new XamlPackageFileTemplate());
                Assert.Contains("Защита сохранена", new System.Windows.Documents.TextRange(restored.ContentStart, restored.ContentEnd).Text);
            }
            container.Resolve<IKeyProvider>().Clear();
            using(var editable = File.Open(protectedArchive, FileMode.Open, FileAccess.Write, FileShare.None)) { }
            using(var editable = File.Open(archive, FileMode.Open, FileAccess.Write, FileShare.None)) { }
            Assert.Empty(listener.Errors);
            Assert.Empty(messages.Errors);
        }
        finally
        {
            window.Close();
            app.Resources.MergedDictionaries.Remove(light);
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            LocalizationManager.SelectCulture("en-US");
        }
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while(!condition())
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), "Explorer operation timed out.");
            await Task.Delay(25);
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static async Task<TreeViewItem> FindTreeItemAsync(TreeView tree, ISystemItem item)
    {
        var scroll = Descendants<ScrollViewer>(tree).First();
        scroll.ScrollToTop();
        for(int page = 0; page < 100; page++)
        {
            tree.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var container = Descendants<TreeViewItem>(tree).FirstOrDefault(node => ReferenceEquals(node.DataContext, item));
            if(container is not null)
            {
                container.BringIntoView();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                return container;
            }
            scroll.PageDown();
        }
        throw new Xunit.Sdk.XunitException("The archive folder was not found inside its physical parent in the tree.");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T: DependencyObject
    {
        for(int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if(child is T result) yield return result;
            foreach(var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Render(Window window, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while(root.Parent is not null && !Directory.Exists(Path.Combine(root.FullName, "CryptoBook.Tests"))) root = root.Parent;
        string folder = Path.Combine(root.FullName, "artifacts", "ArchiveStage2", "screenshots");
        Directory.CreateDirectory(folder);
        using var output = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(output);
    }
    private sealed class BindingErrors: TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if(!string.IsNullOrEmpty(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
    private sealed class RecordingMessages: IMessageService
    {
        internal List<string> Errors { get; } = [];
        internal List<(string Title, string Message)> Confirmations { get; } = [];
        internal bool AllowConfirmation { get; set; }
        public Task<Guid> ShowMessage(string title, string message, bool isCanceled = false)
        {
            if(isCanceled)
            {
                Confirmations.Add((title, message));
                return Task.FromResult(Guid.Empty);
            }
            Errors.Add(title + ": " + message);
            Console.WriteLine("ARCHIVE_UI_MESSAGE: " + title + ": " + message);
            return Task.FromResult(Guid.Empty);
        }
        public void CloseDialog(Guid id) { }
        public bool ShowConfirmation(Guid id) => AllowConfirmation;
    }

    private sealed class NoRecovery: IDocumentRecoveryService
    {
        public bool HasSnapshot => false;
        public void Start() { }
        public Task StopAsync() => Task.CompletedTask;
        public Task<bool> RestoreSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task DeleteSnapshotAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
}
