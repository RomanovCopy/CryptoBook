using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using System.IO;
using System.Reflection;

using Xunit;

namespace CryptoBook.Tests;

public sealed class FileExplorerArchitectureTests
{
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
        Assert.Contains("await OpenPathAsync(selectedPath);", source);

        string pickerSource = File.ReadAllText(FindRepositoryFile(
            "CryptoBook",
            "Services",
            "FileExplorerService.cs"));
        Assert.Contains("normalizedPath[localPrefix.Length..]", pickerSource);
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
