using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;
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

public sealed class BatchRenameWindowTests
{
    [WpfFact]
    public async Task VisibleCompiledWindow_PreviewsRenamesAndUndoes_InBothThemes()
    {
        using var directory = new BatchRenameTests.TestDirectory();
        string a = directory.File("Первый.txt", "A"), b = directory.File("Второй.txt", "B");
        var app = System.Windows.Application.Current ?? new System.Windows.Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Resources["Res"] = new ResourceWrapper();
        foreach(string resource in new[] { "WindowStyles", "ControlStyles", "ButtonStyles", "ComboBoxStyles", "ThemedControls" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/CryptoBook;component/Styles/{resource}.xaml", UriKind.Relative)
            });
        using var container = new Startup().ConfigureServices(app, builder =>
        {
            builder.RegisterInstance<IPinnedDocumentStore>(new JsonPinnedDocumentStore(
                Path.Combine(directory.Path, "pinned.json")));
            builder.RegisterInstance<IRecentDocumentStore>(new JsonRecentDocumentStore(
                Path.Combine(directory.Path, "recent.json")));
        });
        var windows = container.Resolve<IWindowManager>();
        VerifyExplorerCommand(container);
        var pinned = container.Resolve<IPinnedDocumentService>();
        var recent = container.Resolve<IRecentDocumentService>();
        await pinned.PinAsync(a);
        await recent.RecordOpenedAsync(a);
        var session = container.Resolve<IDocumentSession>();
        session.Open(a, new CryptoBook.FileTemplates.PlainTextTemplate());
        session.MarkDirty();
        var listener = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            foreach(string theme in new[] { "Light", "Dark" })
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"/CryptoBook;component/Themes/{theme}Theme.xaml", UriKind.Relative)
                });
                Guid id = windows.CreateWindow<BatchRenameWindow>(new Dictionary<string, object?>
                {
                    [BatchRenameViewModel.FilesContextKey] = new[] { a, b }
                });
                var window = Assert.IsType<BatchRenameWindow>(windows.FindHostWindow(id)!.Window);
                var vm = Assert.IsType<BatchRenameViewModel>(window.DataContext);
                window.Show();
                try
                {
                    await vm.RefreshPreviewAsync();
                    Assert.False(vm.ApplyCommand.CanExecute(null));
                    VerifyMaskButtons(window, vm);
                    var mask = Assert.IsType<TextBox>(window.FindName("NameMaskBox"));
                    mask.Text = "Фото_[C]";
                    Assert.Equal("Фото_[C]", vm.NameMask);
                    Assert.False(vm.ApplyCommand.CanExecute(null)); // stale preview cannot run
                    await vm.RefreshPreviewAsync();
                    Assert.Equal(new[] { "Фото_001.txt", "Фото_002.txt" }, vm.Entries.Select(e => e.NewName));
                    Assert.True(vm.ApplyCommand.CanExecute(null));
                    var combo = Assert.IsType<ComboBox>(window.FindName("CaseCombo"));
                    combo.SelectedIndex = 1;
                    Assert.Equal(1, vm.CaseIndex);
                    combo.SelectedIndex = 0;
                    await vm.RefreshPreviewAsync();
                    window.Width = 760;
                    window.Height = 600;
                    window.UpdateLayout();
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    var grid = Assert.IsType<DataGrid>(window.FindName("PreviewGrid"));
                    Assert.Equal(2, grid.Items.Count);
                    Assert.All(grid.Columns, column => Assert.False(string.IsNullOrWhiteSpace(column.Header?.ToString())));
                    var apply = Assert.IsType<Button>(window.FindName("ApplyButton"));
                    Assert.True(apply.IsEnabled);
                    var bounds = apply.TransformToAncestor(window).TransformBounds(new Rect(apply.RenderSize));
                    Assert.True(bounds.Right <= window.ActualWidth && bounds.Bottom <= window.ActualHeight);
                    foreach(string panelName in new[] { "NameMaskTokens", "ExtensionMaskTokens" })
                    {
                        var panel = Assert.IsType<WrapPanel>(window.FindName(panelName));
                        Assert.All(panel.Children.OfType<Button>(), button =>
                        {
                            var buttonBounds = button.TransformToAncestor(window).TransformBounds(new Rect(button.RenderSize));
                            Assert.True(buttonBounds.Right <= window.ActualWidth && buttonBounds.Bottom <= window.ActualHeight);
                        });
                    }
                    Render(window, theme);
                    await vm.ApplyCommand.ExecuteAsync();
                    Assert.False(vm.IsBusy);
                    Assert.False(vm.ApplyCommand.CanExecute(null));
                    Assert.False(Assert.IsType<WrapPanel>(window.FindName("NameMaskTokens")).IsEnabled);
                    Assert.Equal("A", File.ReadAllText(Path.Combine(directory.Path, "Фото_001.txt")));
                    Assert.Equal("B", File.ReadAllText(Path.Combine(directory.Path, "Фото_002.txt")));
                    Assert.Equal(Path.Combine(directory.Path, "Фото_001.txt"), Assert.Single(pinned.Items).Path);
                    Assert.Equal(Path.Combine(directory.Path, "Фото_001.txt"), session.FilePath);
                    Assert.True(session.IsDirty);
                    Assert.True(vm.UndoCommand.CanExecute(null));
                    await vm.UndoCommand.ExecuteAsync();
                    Assert.Equal("A", File.ReadAllText(a));
                    Assert.Equal("B", File.ReadAllText(b));
                    Assert.Equal(a, Assert.Single(recent.Items).Path);
                    Assert.Equal(a, session.FilePath);
                }
                finally { window.Close(); }
            }
            await VerifyCloseCancelsBeforeWindowDisappears(container, a);
            await VerifyExplorerOpensModalInDisplayedOrder(container, directory.Path);
            Assert.Empty(listener.Errors);
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); }
    }

    private static void VerifyMaskButtons(BatchRenameWindow window, BatchRenameViewModel vm)
    {
        var name = Assert.IsType<TextBox>(window.FindName("NameMaskBox"));
        var extension = Assert.IsType<TextBox>(window.FindName("ExtensionMaskBox"));
        foreach(var (panelName, target, other) in new[]
        {
            ("NameMaskTokens", name, extension),
            ("ExtensionMaskTokens", extension, name)
        })
        {
            var panel = Assert.IsType<WrapPanel>(window.FindName(panelName));
            Button[] buttons = panel.Children.OfType<Button>().ToArray();
            Assert.Equal(new[] { "[N]", "[E]", "[C]", "[DMY]", "[HMS]" }, buttons.Select(b => b.Content));
            foreach(Button button in buttons)
            {
                other.Text = "untouched";
                target.Text = "prefix_old_suffix";
                target.Select(7, 3);
                Assert.True(button.Focus()); // keyboard activation can move focus away from the field
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                string expected = "prefix_" + button.Content + "_suffix";
                Assert.Equal(expected, target.Text);
                Assert.Equal(expected, ReferenceEquals(target, name) ? vm.NameMask : vm.ExtensionMask);
                Assert.Equal("untouched", other.Text);
                Assert.Equal(7 + button.Content.ToString()!.Length, target.CaretIndex);
                Assert.Equal(0, target.SelectionLength);
                Assert.True(target.IsKeyboardFocused);
                Assert.False(string.IsNullOrWhiteSpace(button.ToolTip?.ToString()));
            }
            target.Clear();
            foreach(Button button in buttons)
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("[N][E][C][DMY][HMS]", target.Text);
        }
        name.Text = "[N]";
        extension.Text = "[E]";
    }

    private static async Task VerifyCloseCancelsBeforeWindowDisappears(IContainer container, string path)
    {
        using var scope = container.BeginLifetimeScope(builder =>
        {
            builder.RegisterInstance<IWindowContext>(new WindowContext(new Dictionary<string, object?>
            {
                [BatchRenameViewModel.FilesContextKey] = new[] { path }
            }));
            builder.RegisterInstance<IBatchRenameService>(new WaitingRenameService());
        });
        BatchRenameWindow window;
        using(AmbientScope.Push(scope)) window = scope.Resolve<BatchRenameWindow>();
        var vm = (BatchRenameViewModel)window.DataContext;
        window.Show();
        try
        {
            await vm.RefreshPreviewAsync();
            Task pending = vm.ApplyCommand.ExecuteAsync();
            Assert.True(vm.IsBusy);
            window.Close();
            Assert.True(window.IsVisible);
            await pending;
            Assert.False(vm.IsBusy);
            Assert.True(window.IsVisible);
            Assert.False(vm.UndoCommand.CanExecute(null));
        }
        finally { window.Close(); }
    }

    private static async Task VerifyExplorerOpensModalInDisplayedOrder(IContainer container, string path)
    {
        using var scope = container.BeginLifetimeScope(builder =>
            builder.RegisterInstance<IWindowContext>(new WindowContext(new Dictionary<string, object?>())));
        var vm = scope.Resolve<IFileExplorerViewModel>();
        try
        {
            Assert.True(await scope.Resolve<IFileExplorerModel>().NavigateAsync(path, FileExplorerNavigationMode.Standard));
            var files = vm.ChildrenView.OfType<IFileItem>()
                .Where(f => Path.GetExtension(f.FullPath) == ".txt").ToArray();
            Assert.Equal(2, files.Length);
            vm.SelectedItemsSnapshot = files.Reverse().ToArray();
            Assert.True(vm.BatchRenameCommand.CanExecute(null));
            var checkedDialog = new TaskCompletionSource();
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                BatchRenameWindow? dialog = null;
                try
                {
                    dialog = System.Windows.Application.Current.Windows.OfType<BatchRenameWindow>().Single(w => w.IsVisible);
                    var rename = (BatchRenameViewModel)dialog.DataContext;
                    rename.NameMask = "[DMY]_[HMS]_[C]";
                    await rename.RefreshPreviewAsync();
                    Assert.Equal(files.Select(f => f.FullPath), rename.Entries.Select(e => e.SourcePath));
                    Assert.All(rename.Entries, e => Assert.Matches(@"^\d{8}_\d{6}_\d{3}\.txt$", e.NewName));
                    checkedDialog.SetResult();
                }
                catch(Exception exception) { checkedDialog.SetException(exception); }
                finally { dialog?.Close(); }
            }));
            vm.BatchRenameCommand.Execute(null);
            await checkedDialog.Task;
        }
        finally { vm.Closed.Execute(null); }
    }

    private sealed class WaitingRenameService: IBatchRenameService
    {
        public Task<IReadOnlyList<BatchRenameEntry>> PreviewAsync(IReadOnlyList<string> paths,
            BatchRenameOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BatchRenameEntry>>([new(paths[0], "changed.txt")]);
        public async Task<BatchRenameResult> RenameAsync(IReadOnlyList<BatchRenameEntry> entries,
            IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Expected cancellation");
        }
    }

    private static void VerifyExplorerCommand(IContainer container)
    {
        using var scope = container.BeginLifetimeScope(builder =>
            builder.RegisterInstance<IWindowContext>(new WindowContext(new Dictionary<string, object?>())));
        var vm = scope.Resolve<IFileExplorerViewModel>();
        try
        {
            var file = new FileItem { FullPath = @"C:\files\a.txt", Name = "a.txt",
                Capabilities = StorageProviderCapabilities.Rename };
            var secondFile = new FileItem { FullPath = @"C:\files\b.txt", Name = "b.txt",
                Capabilities = StorageProviderCapabilities.Rename };
            vm.SelectedItemsSnapshot = [file];
            Assert.False(vm.BatchRenameCommand.CanExecute(null));
            Assert.False(vm.BatchRenameCommand.CanExecute(new[] { file }));
            Assert.True(vm.RenameClickCommand.CanExecute(new[] { file }));
            vm.SelectedItemsSnapshot = [file, secondFile];
            Assert.True(vm.BatchRenameCommand.CanExecute(null));
            file.Capabilities = StorageProviderCapabilities.None;
            Assert.True(vm.BatchRenameCommand.CanExecute(null));
            file.IsEditing = true;
            Assert.False(vm.BatchRenameCommand.CanExecute(null));
            file.IsEditing = false;
            file.FullPath = "wpd://device/item";
            Assert.False(vm.BatchRenameCommand.CanExecute(null));
            var folder = scope.Resolve<IDirectoryItem>();
            folder.FullPath = @"C:\files";
            vm.SelectedItemsSnapshot = [folder, secondFile];
            Assert.False(vm.BatchRenameCommand.CanExecute(null));
            vm.SelectedItemsSnapshot = [secondFile];
            Assert.False(vm.BatchRenameCommand.CanExecute(null));
            vm.SelectedItemsSnapshot = [];
            Assert.False(vm.BatchRenameCommand.CanExecute(null));
        }
        finally { vm.Closed.Execute(null); }
    }

    private static void Render(Window window, string theme)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight,
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string folder = Path.Combine(FindRoot(), "artifacts", "batch-rename-smoke");
        Directory.CreateDirectory(folder);
        using var output = File.Create(Path.Combine(folder, theme.ToLowerInvariant() + ".png"));
        encoder.Save(output);
    }

    private static string FindRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while(folder.Parent is not null && !Directory.Exists(Path.Combine(folder.FullName, "CryptoBook.Tests")))
            folder = folder.Parent;
        return folder.FullName;
    }

    private sealed class BindingErrors: TraceListener
    {
        public List<string> Errors { get; } = [];
        public override void Write(string? message) { if(!string.IsNullOrEmpty(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
