using Autofac;

using CryptoBook.DTO;
using CryptoBook.Injections;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System.IO;
using System.Reflection;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerArchitectureTests
{
    [Fact]
    public void FilePropertiesService_RejectsMissingPathBeforeCallingShell()
    {
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"cryptobook-missing-{Guid.NewGuid():N}");

        var result = new WindowsFilePropertiesService().Show(missingPath);

        Assert.False(result.Success);
        Assert.Equal("shell:properties", result.Action);
        Assert.Equal(missingPath, result.Target);
    }

    [Fact]
    public void DocumentEntryPoints_DependOnWorkspaceFileOpenService()
    {
        AssertConstructorUsesWorkspaceOpener(typeof(RecentDocumentsViewModel));
        AssertConstructorUsesWorkspaceOpener(typeof(PinnedDocumentsViewModel));

        string modelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "FileExplorerModel.cs"));
        Assert.Contains("_fileOpenService.OpenAsync(", modelSource);
    }

    [Fact]
    public void PrimaryFileSelection_DoesNotUseWindowsPickerDialogs()
    {
        string[] sourceFiles =
        [
            FindRepositoryFile("CryptoBook", "Services", "FilePickerService.cs"),
            FindRepositoryFile("CryptoBook", "Services", "FolderPickerService.cs"),
            FindRepositoryFile("CryptoBook", "Services", "LocalFolderPickerService.cs"),
            FindRepositoryFile("CryptoBook", "Models", "MediaPlayerModel.cs")
        ];

        foreach(string sourceFile in sourceFiles)
        {
            string source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("OpenFileDialog", source);
            Assert.DoesNotContain("FolderBrowserDialog", source);
        }
    }

    [Fact]
    public void HomeOpenCommand_DelegatesToFileExplorer()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "ViewModels",
            "HomeViewModel.cs"));

        Assert.Contains("menuFileViewModel.OpenFile", source);
        Assert.DoesNotContain("IFilePickerService", source);
    }

    [Fact]
    public void ManageMode_ExposesOpenButtonForCurrentSelection()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Views",
            "FileExplorer.xaml"));

        Assert.Contains("<Setter Property=\"Command\" Value=\"{Binding OpenCommand}\"/>", source);
        Assert.Contains(
            "<Setter Property=\"CommandParameter\" Value=\"{Binding SelectedItemsSnapshot}\"/>",
            source);
        Assert.Contains(
            "<DataTrigger Binding=\"{Binding IsPickerMode}\" Value=\"True\">",
            source);
        Assert.Contains(
            "<Setter Property=\"Command\" Value=\"{Binding ConfirmSelectionCommand}\"/>",
            source);
    }

    [Fact]
    public void ManageMode_ClosesFileExplorerAfterOpeningFile()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "FileExplorerModel.cs"));
        int methodStart = source.IndexOf(
            "private async Task OpenFileAsync(",
            StringComparison.Ordinal);
        int methodEnd = source.IndexOf(
            "public void Execute_ListViewSelectionChangedCommand",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        string methodSource = source[methodStart..methodEnd];
        Assert.Contains("CloseWindow(WindowId)", methodSource);
    }

    [Fact]
    public void PropertiesCommand_UsesNativeShellPropertySheet()
    {
        string viewSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Views",
            "FileExplorer.xaml"));
        string propertiesServiceSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "WindowsFilePropertiesService.cs"));
        string launcherSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "FileLauncherService.cs"));
        string modelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "FileExplorerModel.cs"));
        string viewModelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "ViewModels",
            "FileExplorerViewModel.cs"));

        Assert.Contains("[Common.Properties]", viewSource);
        Assert.Contains("PropertiesCommand", viewSource);
        Assert.Contains("SeeMaskInvokeIdList", propertiesServiceSource);
        Assert.Contains("ShellExecuteEx(ref info)", propertiesServiceSource);
        Assert.DoesNotContain("ShellExecuteEx", launcherSource);
        Assert.Contains("IFilePropertiesService", viewModelSource);
        Assert.DoesNotContain("Execute_PropertiesCommand", modelSource);
    }

    [Fact]
    public void OpenWithCommand_UsesProtectedWorkspaceFlow()
    {
        string modelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "FileExplorerModel.cs"));
        string serviceSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "WorkspaceFileOpenService.cs"));

        Assert.Contains("_fileOpenService", modelSource);
        Assert.Contains(".OpenWithAsync(", modelSource);
        Assert.DoesNotContain(
            "_fileLauncherService.Open(file.FullPath, \"openas\")",
            modelSource);
        Assert.Contains("FileAttributes.ReadOnly", serviceSource);
        Assert.Contains("LaunchOpenWith(protectedCopyPath)", serviceSource);

        string launcherSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "FileLauncherService.cs"));
        Assert.Contains("SHOpenWithDialog", launcherSource);
        Assert.DoesNotContain(
            "fileLauncherService.Open(path, \"openas\")",
            serviceSource);
    }

    [Fact]
    public void SystemOpenWithDialog_RejectsMissingFileBeforeNativeCall()
    {
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"cryptobook-open-with-{Guid.NewGuid():N}.txt");

        LaunchResult result = new FileLauncherService()
            .ShowOpenWithDialog(missingPath);

        Assert.False(result.Success);
        Assert.Equal("shell:open-with-dialog", result.Action);
        Assert.Equal(missingPath, result.Target);
    }

    [Fact]
    public void HomeMediaPlayerCommand_ReusesSideMenuEntryPoint()
    {
        string viewModelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "ViewModels",
            "HomeViewModel.cs"));
        string viewSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "MyPages",
            "Home.xaml"));

        Assert.Contains(
            "OpenMediaPlayer => menuContentViewModel.MediaPlayer",
            viewModelSource);
        Assert.Contains("Command=\"{Binding OpenMediaPlayer}\"", viewSource);
    }

    [Fact]
    public void MediaPlayerOpenCommand_UsesPersistentFileExplorerSelection()
    {
        Type[] parameterTypes = Assert.Single(typeof(CryptoBook.Models.MediaPlayerModel).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        string source = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "MediaPlayerModel.cs"));

        Assert.Contains(typeof(IFileExplorerService), parameterTypes);
        Assert.DoesNotContain(typeof(IFilePickerService), parameterTypes);
        Assert.Contains("_fileExplorerService.ShowFileSelection(", source);
        Assert.Contains("OpenPathAsync(", source);
        Assert.Contains("catalogSelectionPath", source);
        Assert.Contains("OpenSelectedFileAsync(selection)", source);
        Assert.Contains("_windowManager.ActivateWindow(WindowId)", source);

        string pickerSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "FileExplorerService.cs"));
        Assert.Contains(
            "windowManager.CreateSiblingWindow<FileExplorer>(",
            pickerSource);
        Assert.Contains("windowManager.ShowWindow(windowId)", pickerSource);
        Assert.Contains("FileSelectionHandlerContextKey", pickerSource);
    }

    [Fact]
    public void PersistentFileSelection_InvokesHandlerWithoutClosingExplorer()
    {
        string source = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "ViewModels",
            "FileExplorerViewModel.cs"));
        int methodStart = source.IndexOf(
            "private void ConfirmSelection(",
            StringComparison.Ordinal);
        int methodEnd = source.IndexOf(
            "private string? ResolvePickerSelection()",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);
        string methodSource = source[methodStart..methodEnd];
        int handlerCall = methodSource.IndexOf(
            "_fileSelectionHandler(CreateMediaCatalogSelection(",
            StringComparison.Ordinal);
        int earlyReturn = methodSource.IndexOf(
            "return;",
            handlerCall,
            StringComparison.Ordinal);
        int closeWindow = methodSource.IndexOf(
            "CloseWindow(WindowId)",
            StringComparison.Ordinal);

        Assert.True(handlerCall >= 0);
        Assert.True(earlyReturn > handlerCall);
        Assert.True(closeWindow > earlyReturn);
    }

    [Fact]
    public void FileExplorerService_PersistentSelection_IsModelessAndKeepsHandler()
    {
        var windows = new WindowManagerStub();
        var service = new FileExplorerService(
            windows,
            new FileManagerService([]));
        Action<MediaCatalogSelection> handler = _ => { };

        service.ShowFileSelection(@"C:\Media", handler);

        Assert.Equal(typeof(CryptoBook.Views.FileExplorer), windows.CreatedType);
        Assert.True(windows.CreatedAsSibling);
        Assert.Equal(1, windows.ShowCount);
        Assert.Equal(0, windows.ShowDialogCount);
        Assert.Equal(
            FileExplorerMode.SelectFile,
            windows.Arguments?[FileExplorerService.ModeContextKey]);
        Assert.Same(
            handler,
            windows.Arguments?[FileExplorerService.FileSelectionHandlerContextKey]);
    }

    [WpfFact]
    public void PersistentFileSelection_UsesHandlerWithoutDialogResult()
    {
        var app = System.Windows.Application.Current ??
            new System.Windows.Application();
        using IContainer container = new Startup().ConfigureServices(app);
        MediaCatalogSelection? selection = null;
        var context = new WindowContext(new Dictionary<string, object?>
        {
            [FileExplorerService.ModeContextKey] = FileExplorerMode.SelectFile,
            [FileExplorerService.FileSelectionHandlerContextKey] =
                (Action<MediaCatalogSelection>)(value => selection = value)
        });
        using ILifetimeScope scope = container.BeginLifetimeScope(builder =>
            builder.RegisterInstance<IWindowContext>(context)
                .As<IWindowContext>()
                .SingleInstance());
        IFileExplorerViewModel viewModel = scope.Resolve<IFileExplorerViewModel>();
        var file = new FileItem
        {
            Name = "video.mp4",
            FullPath = @"C:\Media\video.mp4"
        };

        try
        {
            viewModel.SelectedListItem = file;
            viewModel.SelectedItemsSnapshot = [file];

            Assert.True(viewModel.ConfirmSelectionCommand.CanExecute(null));
            viewModel.ConfirmSelectionCommand.Execute(null);

            Assert.NotNull(selection);
            Assert.Equal(file.FullPath, selection.SelectedPath);
            Assert.Empty(selection.FilePaths);
            Assert.False(viewModel.HasResult);
        }
        finally
        {
            viewModel.Closed.Execute(null);
        }
    }

    [Fact]
    public void ExplorerAndCoordinator_DoNotInterpretProviderLocatorsAsNativePaths()
    {
        string modelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "FileExplorerModel.cs"));
        string coordinatorSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "FileOperationCoordinator.cs"));

        foreach(string source in new[] { modelSource, coordinatorSource })
        {
            Assert.DoesNotContain("Path.Get", source);
            Assert.DoesNotContain("Path.Combine", source);
            Assert.DoesNotContain("Path.Exists", source);
            Assert.DoesNotContain("File.Exists", source);
            Assert.DoesNotContain("Directory.Exists", source);
        }
        Assert.Contains("IStorageFacade", modelSource);
        Assert.Contains("IStorageFacade", coordinatorSource);
    }

    [Fact]
    public void AndroidDeletion_RequiresExplicitPermanentDeleteConfirmation()
    {
        string modelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "FileExplorerModel.cs"));

        Assert.Contains("item => !item.Location.IsLocal", modelSource);
        Assert.Contains("Explorer.PermanentDeleteAndroidWarning", modelSource);
        Assert.Contains("ShowConfirmation(confirmationId)", modelSource);
    }

    [Fact]
    public void PickerStatus_UsesHumanReadablePathAndDynamicLabel()
    {
        string viewSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Views",
            "FileExplorer.xaml"));
        string modelSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "FileExplorerModel.cs"));

        Assert.Contains(
            "SelectedItem.DisplayPath, ElementName=listview",
            viewSource);
        Assert.DoesNotContain(
            "SelectedItem.FullPath, ElementName=listview",
            viewSource);
        Assert.Contains("Text=\"{Binding PickerPathLabel}\"", viewSource);
        Assert.Contains(
            "Text=\"{Binding PickerSelectionDisplayPath}\"",
            viewSource);
        Assert.DoesNotContain(
            "Text=\"{Binding PickerSelectionPath}\"",
            viewSource);
        Assert.Contains(
            "AddressText = GetDisplayPath(targetPath);",
            modelSource);
        Assert.Contains(
            "ResolveDisplayPath(",
            modelSource);
    }

    private static void AssertConstructorUsesWorkspaceOpener(Type type)
    {
        ConstructorInfo constructor = Assert.Single(type.GetConstructors());
        Type[] parameterTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(IWorkspaceFileOpenService), parameterTypes);
        Assert.DoesNotContain(typeof(IDocumentSwitchCoordinator), parameterTypes);
    }

    private sealed class WindowManagerStub: IWindowManager
    {
        public Type? CreatedType { get; private set; }
        public bool CreatedAsSibling { get; private set; }
        public IReadOnlyDictionary<string, object?>? Arguments { get; private set; }
        public int ShowCount { get; private set; }
        public int ShowDialogCount { get; private set; }

        public Guid CreateWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T: System.Windows.Window
        {
            CreatedType = typeof(T);
            Arguments = args;
            return Guid.NewGuid();
        }

        public Guid CreateSiblingWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T: System.Windows.Window
        {
            CreatedAsSibling = true;
            return CreateWindow<T>(args);
        }

        public TResult? GetResult<TResult>(Guid guid) => default;
        public void ShowWindow(Guid windowId) => ShowCount++;
        public void ShowWindowDialog(Guid windowId) => ShowDialogCount++;
        public void ActivateWindow(Guid windowId) { }
        public void CloseWindow(Guid windowId) { }
        public bool IsWindowOpen(Guid windowId) => false;
        public WindowHost? FindHostWindow(Guid windowId) => null;
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while(directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. parts]);
            if(File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
