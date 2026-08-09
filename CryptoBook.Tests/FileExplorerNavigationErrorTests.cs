using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Services;
using CryptoBook.ViewModels;

using System;
using System.IO;
using System.Xml.Linq;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class FileExplorerNavigationErrorTests
    {
        [Fact]
        public void Classifier_RecognizesEveryNavigationFailureCategory()
        {
            Assert.Equal(
                FileExplorerNavigationErrorKind.DirectoryNotFound,
                FileExplorerNavigationErrorClassifier.Classify(
                    new DirectoryNotFoundException(),
                    @"C:\Missing"));
            Assert.Equal(
                FileExplorerNavigationErrorKind.AccessDenied,
                FileExplorerNavigationErrorClassifier.Classify(
                    new IOException(
                        "wrapped",
                        new UnauthorizedAccessException()),
                    @"C:\Protected"));
            Assert.Equal(
                FileExplorerNavigationErrorKind.DriveNotReady,
                FileExplorerNavigationErrorClassifier.Classify(
                    new NativeIOException(21),
                    @"D:\"));
            Assert.Equal(
                FileExplorerNavigationErrorKind.NetworkResourceUnavailable,
                FileExplorerNavigationErrorClassifier.Classify(
                    new DirectoryNotFoundException(),
                    @"\\server\share"));
            Assert.Equal(
                FileExplorerNavigationErrorKind.OperationCanceled,
                FileExplorerNavigationErrorClassifier.Classify(
                    new OperationCanceledException(),
                    @"C:\Work"));
        }

        [Fact]
        public void ItemFilter_MatchesFullFileNameWithoutChangingSource()
        {
            var file = new FileItem
            {
                Name = "Quarterly Report",
                Extension = ".PDF"
            };
            ISystemItem[] children = [file];

            Assert.True(FileExplorerItemFilter.Matches(file, "report.pdf"));
            Assert.False(FileExplorerItemFilter.Matches(file, "notes"));
            Assert.Single(children);
            Assert.Same(file, children[0]);
        }

        [Fact]
        public void ExplorerContract_ExposesCollectionViewAndBoundedDebounce()
        {
            var viewProperty = typeof(IFileExplorerViewModel).GetProperty(
                nameof(IFileExplorerViewModel.ChildrenView));

            Assert.NotNull(viewProperty);
            Assert.Equal(typeof(System.ComponentModel.ICollectionView),
                viewProperty!.PropertyType);
            Assert.InRange(
                FileExplorerViewModel.FilterDebounceMilliseconds,
                150,
                250);
        }

        [Fact]
        public void FileExplorer_ListBindsToViewInsteadOfMutatingChildren()
        {
            string xamlPath = FindRepositoryFile(
                "CryptoBook",
                "Views",
                "FileExplorer.xaml");
            XDocument document = XDocument.Load(xamlPath);
            XNamespace presentation =
                "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XElement listView = Assert.Single(
                document.Descendants(presentation + "ListView"));

            Assert.Equal(
                "{Binding ChildrenView}",
                (string?)listView.Attribute("ItemsSource"));
            Assert.DoesNotContain(
                "SelectedItem.Children",
                document.ToString(SaveOptions.DisableFormatting));
        }

        private static string FindRepositoryFile(params string[] parts)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while(directory is not null)
            {
                string candidate = Path.Combine(
                    directory.FullName,
                    Path.Combine(parts));
                if(File.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }

            throw new FileNotFoundException(Path.Combine(parts));
        }

        private sealed class NativeIOException: IOException
        {
            public NativeIOException(int nativeErrorCode)
            {
                HResult = unchecked((int)0x80070000) | nativeErrorCode;
            }
        }
    }
}
