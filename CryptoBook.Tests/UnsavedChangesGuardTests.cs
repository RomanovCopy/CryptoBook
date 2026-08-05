using CryptoBook.Interfaces;
using CryptoBook.Services;

using System.Windows.Documents;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class UnsavedChangesGuardTests
    {
        [Fact]
        public async Task CleanDocument_ProceedsWithoutPromptOrSave()
        {
            var session = new DocumentSessionStub { IsDirty = false };
            var saver = new DocumentSaverStub();
            var dialogs = new DialogServiceStub();
            var guard = new UnsavedChangesGuard(session, saver, dialogs);

            bool result = await guard.CanProceedAsync();

            Assert.True(result);
            Assert.Equal(0, dialogs.SwitchPromptCount);
            Assert.Equal(0, saver.SaveCount);
        }

        [Fact]
        public async Task Cancel_KeepsCurrentDocumentWithoutSaving()
        {
            var session = new DocumentSessionStub { IsDirty = true };
            var saver = new DocumentSaverStub();
            var dialogs = new DialogServiceStub
            {
                Choice = UnsavedChangesChoice.Cancel
            };
            var guard = new UnsavedChangesGuard(session, saver, dialogs);

            bool result = await guard.CanProceedAsync();

            Assert.False(result);
            Assert.True(session.IsDirty);
            Assert.Equal(0, saver.SaveCount);
        }

        [Fact]
        public async Task Discard_ProceedsWithoutChangingCurrentDirtyState()
        {
            var session = new DocumentSessionStub { IsDirty = true };
            var saver = new DocumentSaverStub();
            var dialogs = new DialogServiceStub
            {
                Choice = UnsavedChangesChoice.Discard
            };
            var guard = new UnsavedChangesGuard(session, saver, dialogs);

            bool result = await guard.CanProceedAsync();

            Assert.True(result);
            Assert.True(session.IsDirty);
            Assert.Equal(0, saver.SaveCount);
        }

        [Fact]
        public async Task Save_ProceedsOnlyAfterDocumentBecomesClean()
        {
            var session = new DocumentSessionStub { IsDirty = true };
            var saver = new DocumentSaverStub
            {
                Save = () =>
                {
                    session.IsDirty = false;
                    return true;
                }
            };
            var guard = new UnsavedChangesGuard(
                session,
                saver,
                new DialogServiceStub
                {
                    Choice = UnsavedChangesChoice.Save
                });

            bool result = await guard.CanProceedAsync();

            Assert.True(result);
            Assert.Equal(1, saver.SaveCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FailedOrStaleSave_BlocksSwitch(bool saveResult)
        {
            var session = new DocumentSessionStub { IsDirty = true };
            var saver = new DocumentSaverStub
            {
                Save = () => saveResult
            };
            var guard = new UnsavedChangesGuard(
                session,
                saver,
                new DialogServiceStub
                {
                    Choice = UnsavedChangesChoice.Save
                });

            bool result = await guard.CanProceedAsync();

            Assert.False(result);
            Assert.True(session.IsDirty);
            Assert.Equal(1, saver.SaveCount);
        }

        private sealed class DocumentSaverStub: ICurrentDocumentSaver
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

        private sealed class DialogServiceStub: IDocumentDialogService
        {
            public UnsavedChangesChoice Choice { get; init; }
            public int SwitchPromptCount { get; private set; }

            public bool ConfirmRecovery() => false;
            public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() =>
                Choice;
            public UnsavedChangesChoice ConfirmSwitchWithUnsavedChanges()
            {
                SwitchPromptCount++;
                return Choice;
            }
            public void ShowRecoveryError(Exception exception)
            {
            }
            public void ShowRecoveryCleanupError(Exception exception)
            {
            }
        }

        private sealed class DocumentSessionStub: IDocumentSession
        {
            public event System.ComponentModel.PropertyChangedEventHandler?
                PropertyChanged
            {
                add { }
                remove { }
            }

            public string? FilePath { get; private set; }
            public string DisplayName { get; private set; } = string.Empty;
            public IFileTemplate? Template { get; private set; }
            public bool IsDirty { get; set; }
            public long Revision { get; private set; }
            public long SavedRevision { get; private set; }
            public bool HasDocument => IsDirty || FilePath is not null;

            public void Open(string filePath, IFileTemplate template)
            {
                FilePath = filePath;
                Template = template;
                IsDirty = false;
            }
            public void Open(
                string filePath,
                IFileTemplate template,
                FlowDocument document) => Open(filePath, template);
            public void Close() => FilePath = null;
            public void MarkDirty() => IsDirty = true;
            public void MarkSaved(string filePath, IFileTemplate template) =>
                Open(filePath, template);
            public void MarkSaved(
                string filePath,
                IFileTemplate template,
                long savedRevision) => Open(filePath, template);
            public void Rename(string filePath) => FilePath = filePath;
            public void SetDisplayName(string displayName) =>
                DisplayName = displayName;
        }
    }
}
