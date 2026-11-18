using Autofac;

using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;
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

public sealed class ArchiveOperationWindowTests
{
    [WpfFact]
    public async Task CompiledDialogs_BindInFourLanguagesAndBothThemes_AndRunEncryptedRoundTrip()
    {
        MemorySettingsProvider.Install();
        using var directory = new ArchiveTestDirectory();
        string sourceFolder = Directory.CreateDirectory(Path.Combine(directory.Path, "Выбранная папка")).FullName;
        string source = Path.Combine(sourceFolder, "Документ.txt");
        await File.WriteAllTextAsync(source, "Тест архива 🔐");
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
            builder.RegisterInstance<IArchiveService>(directory.CreateService());
            builder.RegisterInstance<IPinnedDocumentStore>(new JsonPinnedDocumentStore(Path.Combine(directory.Path, "pinned.json")));
            builder.RegisterInstance<IRecentDocumentStore>(new JsonRecentDocumentStore(Path.Combine(directory.Path, "recent.json")));
        });
        var windows = container.Resolve<IWindowManager>();
        VerifyExplorerCommands(container, source);
        var listener = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            foreach(string culture in new[] { "ru-RU", "en-US", "uk-UA", "de-DE" })
            foreach(string theme in new[] { "Light", "Dark" })
            {
                LocalizationManager.SelectCulture(culture);
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri($"/CryptoBook;component/Themes/{theme}Theme.xaml", UriKind.Relative)
                };
                app.Resources.MergedDictionaries.Add(dictionary);
                try
                {
                    foreach(ArchiveAction action in Enum.GetValues<ArchiveAction>())
                    {
                        string archive = Path.Combine(directory.Path, "archive.7z");
                        Guid id = windows.CreateWindow<ArchiveOperationWindow>(new Dictionary<string, object?>
                        {
                            [ArchiveOperationViewModel.SourcesContextKey] = new[] { action == ArchiveAction.Create ? sourceFolder : archive },
                            [ArchiveOperationViewModel.ActionContextKey] = action
                        });
                        var window = Assert.IsType<ArchiveOperationWindow>(windows.FindHostWindow(id)!.Window);
                        var vm = Assert.IsType<ArchiveOperationViewModel>(window.DataContext);
                        window.Show();
                        try
                        {
                            Assert.DoesNotContain("Archive.", window.Title);
                            Assert.True(vm.RunCommand.CanExecute(null));
                            var password = Assert.IsType<PasswordBox>(window.FindName("ArchivePassword"));
                            password.Password = "проверка пароля 🔐";
                            Assert.Equal(password.Password.Length, vm.Password!.Length);
                            vm.FormatIndex = 1;
                            if(action == ArchiveAction.Create) vm.OutputName = "archive.7z";
                            if(action == ArchiveAction.Extract) vm.OutputName = "restored";
                            window.Width = 520;
                            window.Height = 460;
                            window.UpdateLayout();
                            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                            var run = Assert.IsType<Button>(window.FindName("RunButton"));
                            var bounds = run.TransformToAncestor(window).TransformBounds(new Rect(run.RenderSize));
                            Assert.True(bounds.Right <= window.ActualWidth && bounds.Bottom <= window.ActualHeight);
                            Render(window, culture + "-" + theme + "-" + action);
                            if(culture == "ru-RU" && theme == "Light")
                            {
                                await vm.RunCommand.ExecuteAsync();
                                Assert.False(vm.IsBusy);
                                Assert.False(vm.CanEdit, vm.Status);
                                Assert.False(vm.RunCommand.CanExecute(null));
                                Assert.Empty(password.Password);
                                Assert.Contains(action == ArchiveAction.Test ? ArchivePathPolicy.Text("TestSuccess") : directory.Path, vm.Status);
                                Assert.DoesNotContain("проверка пароля", vm.Status);
                            }
                        }
                        finally { window.Close(); }
                    }
                }
                finally { app.Resources.MergedDictionaries.Remove(dictionary); }
            }
            Assert.Equal("Тест архива 🔐", await File.ReadAllTextAsync(Path.Combine(directory.Path, "restored", "Выбранная папка", "Документ.txt")));
            Assert.Empty(listener.Errors);
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); }
    }

    private static void VerifyExplorerCommands(IContainer container, string source)
    {
        using var scope = container.BeginLifetimeScope(builder =>
            builder.RegisterInstance<IWindowContext>(new WindowContext(new Dictionary<string, object?>())));
        var vm = scope.Resolve<IFileExplorerViewModel>();
        try
        {
            Assert.False(vm.CreateArchiveCommand.CanExecute(null));
            var file = new FileItem { FullPath = source, Name = Path.GetFileName(source), Capabilities = StorageProviderCapabilities.Read };
            vm.SelectedItemsSnapshot = [file];
            Assert.True(vm.CreateArchiveCommand.CanExecute(null));
            Assert.False(vm.ExtractArchiveCommand.CanExecute(null));
            file.FullPath = Path.ChangeExtension(source, ".rar");
            Assert.True(vm.ExtractArchiveCommand.CanExecute(null));
            Assert.True(vm.TestArchiveCommand.CanExecute(null));
            file.IsEditing = true;
            Assert.False(vm.CreateArchiveCommand.CanExecute(null));
            Assert.False(vm.ExtractArchiveCommand.CanExecute(null));
            file.IsEditing = false;
            file.FullPath = "wpd://device/file.zip";
            Assert.False(vm.CreateArchiveCommand.CanExecute(null));
            Assert.False(vm.TestArchiveCommand.CanExecute(null));
        }
        finally { vm.Closed.Execute(null); }
    }

    private static void Render(Window window, string name)
    {
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while(root.Parent is not null && !Directory.Exists(Path.Combine(root.FullName, "CryptoBook.Tests"))) root = root.Parent;
        string folder = Path.Combine(root.FullName, "artifacts", "ArchiveStage1", "screenshots");
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
}
