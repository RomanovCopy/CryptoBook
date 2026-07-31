using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.ComponentModel;
using System.IO;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class DocumentCloseCoordinatorTests
    {
        [Fact]
        public async Task InitializeAsync_RestoresSnapshot_AndStartsRecovery()
        {
            var recovery = new TestRecoveryService
            {
                HasSnapshot = true
            };
            var dialogs = new TestDialogService
            {
                Recover = true
            };
            var coordinator = CreateCoordinator(
                new TestDocumentSession(),
                recovery,
                dialogs);

            await coordinator.InitializeAsync();

            Assert.Equal(1, recovery.RestoreCount);
            Assert.Equal(0, recovery.DeleteCount);
            Assert.Equal(1, recovery.StartCount);
        }

        [Fact]
        public async Task InitializeAsync_ReportsRestoreFailure_AndStillStarts()
        {
            var expected = new IOException("damaged snapshot");
            var recovery = new TestRecoveryService
            {
                HasSnapshot = true,
                RestoreException = expected
            };
            var dialogs = new TestDialogService
            {
                Recover = true
            };
            var coordinator = CreateCoordinator(
                new TestDocumentSession(),
                recovery,
                dialogs);

            await coordinator.InitializeAsync();

            Assert.Same(expected, dialogs.RecoveryError);
            Assert.Equal(1, recovery.StartCount);
        }

        [Fact]
        public async Task InitializeAsync_DeclinedRecovery_DeletesSnapshot()
        {
            var recovery = new TestRecoveryService
            {
                HasSnapshot = true
            };
            var coordinator = CreateCoordinator(
                new TestDocumentSession(),
                recovery,
                new TestDialogService { Recover = false });

            await coordinator.InitializeAsync();

            Assert.Equal(0, recovery.RestoreCount);
            Assert.Equal(1, recovery.DeleteCount);
            Assert.Equal(1, recovery.StartCount);
        }

        [Fact]
        public async Task InitializeAsync_DeleteFailure_IsReported_AndStillStarts()
        {
            var expected = new IOException("locked snapshot");
            var recovery = new TestRecoveryService
            {
                HasSnapshot = true,
                DeleteException = expected
            };
            var dialogs = new TestDialogService { Recover = false };
            var coordinator = CreateCoordinator(
                new TestDocumentSession(),
                recovery,
                dialogs);

            await coordinator.InitializeAsync();

            Assert.Same(expected, dialogs.RecoveryCleanupError);
            Assert.Equal(1, recovery.StartCount);
        }

        [Fact]
        public async Task TryApproveCloseAsync_Cancel_KeepsWindowOpen()
        {
            var session = new TestDocumentSession { IsDirty = true };
            var recovery = new TestRecoveryService();
            var dialogs = new TestDialogService
            {
                CloseChoice = UnsavedChangesChoice.Cancel
            };
            var coordinator = CreateCoordinator(
                session,
                recovery,
                dialogs);

            bool approved = await coordinator.TryApproveCloseAsync();

            Assert.False(approved);
            Assert.False(coordinator.IsCloseApproved);
            Assert.Equal(0, recovery.DeleteCount);
        }

        [Fact]
        public async Task TryApproveCloseAsync_Save_ApprovesAfterDocumentIsClean()
        {
            var session = new TestDocumentSession { IsDirty = true };
            var recovery = new TestRecoveryService();
            var saver = new TestDocumentSaver
            {
                Save = () =>
                {
                    session.IsDirty = false;
                    return true;
                }
            };
            var coordinator = new DocumentCloseCoordinator(
                session,
                saver,
                recovery,
                new TestDialogService
                {
                    CloseChoice = UnsavedChangesChoice.Save
                });

            bool approved = await coordinator.TryApproveCloseAsync();

            Assert.True(approved);
            Assert.True(coordinator.IsCloseApproved);
            Assert.Equal(1, saver.SaveCount);
            Assert.Equal(1, recovery.DeleteCount);
        }

        [Fact]
        public async Task TryApproveCloseAsync_SaveThatLeavesDocumentDirty_IsRejected()
        {
            var session = new TestDocumentSession { IsDirty = true };
            var recovery = new TestRecoveryService();
            var saver = new TestDocumentSaver();
            var coordinator = new DocumentCloseCoordinator(
                session,
                saver,
                recovery,
                new TestDialogService
                {
                    CloseChoice = UnsavedChangesChoice.Save
                });

            bool approved = await coordinator.TryApproveCloseAsync();

            Assert.False(approved);
            Assert.False(coordinator.IsCloseApproved);
            Assert.Equal(0, recovery.DeleteCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TryApproveCloseAsync_DiscardOrClean_DeletesSnapshot(
            bool isDirty)
        {
            var recovery = new TestRecoveryService();
            var coordinator = CreateCoordinator(
                new TestDocumentSession { IsDirty = isDirty },
                recovery,
                new TestDialogService
                {
                    CloseChoice = UnsavedChangesChoice.Discard
                });

            bool approved = await coordinator.TryApproveCloseAsync();

            Assert.True(approved);
            Assert.True(coordinator.IsCloseApproved);
            Assert.Equal(1, recovery.DeleteCount);
        }

        [Fact]
        public async Task TryApproveCloseAsync_DeleteFailure_IsReported_AndApproves()
        {
            var expected = new IOException("locked snapshot");
            var recovery = new TestRecoveryService
            {
                DeleteException = expected
            };
            var dialogs = new TestDialogService();
            var coordinator = CreateCoordinator(
                new TestDocumentSession(),
                recovery,
                dialogs);

            bool approved = await coordinator.TryApproveCloseAsync();

            Assert.True(approved);
            Assert.True(coordinator.IsCloseApproved);
            Assert.Same(expected, dialogs.RecoveryCleanupError);
        }

        private static DocumentCloseCoordinator CreateCoordinator(
            TestDocumentSession session,
            TestRecoveryService recovery,
            TestDialogService dialogs) =>
            new(
                session,
                new TestDocumentSaver(),
                recovery,
                dialogs);

        private sealed class TestDialogService: IDocumentDialogService
        {
            public bool Recover { get; init; }
            public UnsavedChangesChoice CloseChoice { get; init; }
            public Exception? RecoveryError { get; private set; }
            public Exception? RecoveryCleanupError { get; private set; }

            public bool ConfirmRecovery() => Recover;

            public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() =>
                CloseChoice;

            public void ShowRecoveryError(Exception exception) =>
                RecoveryError = exception;

            public void ShowRecoveryCleanupError(Exception exception) =>
                RecoveryCleanupError = exception;
        }

        private sealed class TestDocumentSaver: ICurrentDocumentSaver
        {
            public Func<bool> Save { get; init; } = () => true;
            public int SaveCount { get; private set; }

            public Task<bool> TrySaveCurrentAsync(
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SaveCount++;
                return Task.FromResult(Save());
            }
        }

        private sealed class TestRecoveryService: IDocumentRecoveryService
        {
            public bool HasSnapshot { get; init; }
            public Exception? RestoreException { get; init; }
            public Exception? DeleteException { get; init; }
            public int StartCount { get; private set; }
            public int RestoreCount { get; private set; }
            public int DeleteCount { get; private set; }

            public void Start() => StartCount++;

            public Task<bool> RestoreSnapshotAsync(
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RestoreCount++;
                return RestoreException is null
                    ? Task.FromResult(true)
                    : Task.FromException<bool>(RestoreException);
            }

            public Task DeleteSnapshotAsync()
            {
                DeleteCount++;
                return DeleteException is null
                    ? Task.CompletedTask
                    : Task.FromException(DeleteException);
            }

            public void Dispose()
            {
            }
        }

        private sealed class TestDocumentSession: IDocumentSession
        {
            public event PropertyChangedEventHandler? PropertyChanged;

            public string? FilePath { get; private set; }
            public string DisplayName { get; private set; } = "Document";
            public IFileTemplate? Template { get; private set; }
            public bool IsDirty { get; set; }
            public long Revision { get; private set; }
            public long SavedRevision { get; private set; }

            public void Open(string filePath, IFileTemplate template)
            {
                FilePath = filePath;
                Template = template;
                IsDirty = false;
            }

            public void MarkDirty()
            {
                IsDirty = true;
                Revision++;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(IsDirty)));
            }

            public void MarkSaved(
                string filePath,
                IFileTemplate template) =>
                MarkSaved(filePath, template, Revision);

            public void MarkSaved(
                string filePath,
                IFileTemplate template,
                long savedRevision)
            {
                FilePath = filePath;
                Template = template;
                SavedRevision = savedRevision;
                IsDirty = false;
            }

            public void Rename(string filePath) => FilePath = filePath;

            public void SetDisplayName(string displayName) =>
                DisplayName = displayName;
        }
    }
}
