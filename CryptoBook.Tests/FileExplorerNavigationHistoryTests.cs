using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FileExplorerNavigationHistoryTests
    {
        [Fact]
        public void ExplorerContract_SeparatesEditableAddressFromReadOnlyCurrentPath()
        {
            var currentPath = typeof(IFileExplorerModel).GetProperty(
                nameof(IFileExplorerModel.CurrentPath));
            var addressText = typeof(IFileExplorerModel).GetProperty(
                nameof(IFileExplorerModel.AddressText));

            Assert.NotNull(currentPath);
            Assert.False(currentPath!.CanWrite);
            Assert.NotNull(addressText);
            Assert.True(addressText!.CanWrite);
        }

        [Fact]
        public void Commit_StoresNormalizedPathsAndMovesBetweenStacks()
        {
            var history = new FileExplorerNavigationHistory();
            string first = Path.GetFullPath(@"C:\Work");
            string second = Path.GetFullPath(@"C:\Work\Documents");

            history.Commit(null, first, FileExplorerNavigationMode.Restore);
            history.Commit(first + Path.DirectorySeparatorChar, second, FileExplorerNavigationMode.Standard);

            Assert.Equal([first], history.BackPaths);
            Assert.Empty(history.ForwardPaths);

            history.Commit(second, first + Path.DirectorySeparatorChar, FileExplorerNavigationMode.Back);

            Assert.Empty(history.BackPaths);
            Assert.Equal([second], history.ForwardPaths);

            history.Commit(first, second, FileExplorerNavigationMode.Forward);

            Assert.Equal([first], history.BackPaths);
            Assert.Empty(history.ForwardPaths);
        }

        [Fact]
        public void StandardNavigation_AfterBack_ClearsForwardHistory()
        {
            var history = new FileExplorerNavigationHistory();
            string first = Path.GetFullPath(@"C:\One");
            string second = Path.GetFullPath(@"C:\Two");
            string third = Path.GetFullPath(@"C:\Three");

            history.Commit(null, first, FileExplorerNavigationMode.Restore);
            history.Commit(first, second, FileExplorerNavigationMode.Standard);
            history.Commit(second, first, FileExplorerNavigationMode.Back);
            Assert.True(history.CanGoForward);

            history.Commit(first, third, FileExplorerNavigationMode.Standard);

            Assert.False(history.CanGoForward);
            Assert.Equal([first], history.BackPaths);
        }

        [Fact]
        public void StandardNavigation_ToSameNormalizedPath_DoesNotCreateEntry()
        {
            var history = new FileExplorerNavigationHistory();
            string path = Path.GetFullPath(@"C:\Work");

            history.Commit(null, path, FileExplorerNavigationMode.Restore);
            history.Commit(
                path + Path.DirectorySeparatorChar,
                Path.Combine(path, "."),
                FileExplorerNavigationMode.Standard);

            Assert.False(history.CanGoBack);
            Assert.False(history.CanGoForward);
        }

        [Fact]
        public void StandardNavigation_ToCurrentPath_PreservesForwardHistory()
        {
            var history = new FileExplorerNavigationHistory();
            string first = Path.GetFullPath(@"C:\One");
            string second = Path.GetFullPath(@"C:\Two");

            history.Commit(null, first, FileExplorerNavigationMode.Restore);
            history.Commit(first, second, FileExplorerNavigationMode.Standard);
            history.Commit(second, first, FileExplorerNavigationMode.Back);

            history.Commit(first, first + Path.DirectorySeparatorChar, FileExplorerNavigationMode.Standard);

            Assert.True(history.CanGoForward);
            Assert.Equal(second, history.ForwardPath);
        }
    }
}
