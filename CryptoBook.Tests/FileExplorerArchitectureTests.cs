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
    public void ManageMode_KeepsFileExplorerOpenAfterOpeningFile()
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
        Assert.DoesNotContain("CloseWindow(WindowId)", methodSource);
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
    public void MediaPlayerOpenCommand_UsesPickerResultToOpenSelectedFile()
    {
        Type[] parameterTypes = Assert.Single(typeof(CryptoBook.Models.MediaPlayerModel).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        string source = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Models",
            "MediaPlayerModel.cs"));

        Assert.Contains(typeof(IFilePickerService), parameterTypes);
        Assert.DoesNotContain(typeof(IFileExplorerService), parameterTypes);
        Assert.Contains("_filePickerService.PickFileAsync(", source);
        Assert.Contains(
            "OpenPathAsync(initialPath, autoPlay: true)",
            source);
        Assert.Contains(
            "await OpenPathAsync(selectedPath, autoPlay: true);",
            source);

        string pickerSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "FileExplorerService.cs"));
        Assert.Contains("normalizedPath[localPrefix.Length..]", pickerSource);
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
    public void SelectedStatus_UsesHumanReadableDisplayPath()
    {
        string viewSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Views",
            "FileExplorer.xaml"));

        Assert.Contains(
            "SelectedItem.DisplayPath, ElementName=listview",
            viewSource);
        Assert.DoesNotContain(
            "SelectedItem.FullPath, ElementName=listview",
            viewSource);
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
