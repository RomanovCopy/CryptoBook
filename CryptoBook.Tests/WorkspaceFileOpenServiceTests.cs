using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.IO;
using System.Security.Cryptography;
using System.Windows;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class WorkspaceFileOpenServiceTests: IDisposable
    {
        private readonly string testDirectory = Path.Combine(
            Path.GetTempPath(),
            "CryptoBook.Tests",
            Guid.NewGuid().ToString("N"));

        public WorkspaceFileOpenServiceTests()
        {
            Directory.CreateDirectory(testDirectory);
        }

        [Fact]
        public async Task SupportedEncryptedFile_IsOpenedInsideCryptoBook()
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(),
                new SecureFileProcessorStub(".txt"),
                new KeyRequestStub(),
                new WindowManagerStub(),
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                new DocumentDialogServiceStub());

            WorkspaceFileOpenResult result = await service.OpenAsync(sourcePath);
            string openedPath = Assert.IsType<string>(internalOpener.DecryptedPath);

            Assert.True(result.Success);
            Assert.True(result.OpenedInternally);
            Assert.NotEqual(sourcePath, openedPath);
            Assert.Equal(".txt", Path.GetExtension(openedPath));
            Assert.Equal(sourcePath, internalOpener.EncryptedPath);
            Assert.IsType<PlainTextTemplate>(internalOpener.ContentTemplate);
            Assert.True(internalOpener.SourceIsEncrypted);
            Assert.Null(launcher.OpenedPath);
            Assert.False(File.Exists(openedPath));

            service.Dispose();
        }

        [Fact]
        public async Task SupportedPlainFile_IsOpenedInsideCryptoBook()
        {
            string sourcePath = Path.Combine(testDirectory, "notes.txt");
            await File.WriteAllTextAsync(sourcePath, "search result");
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(encrypted: false),
                new SecureFileProcessorStub(".txt"),
                new KeyRequestStub(),
                new WindowManagerStub(),
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                new DocumentDialogServiceStub());

            WorkspaceFileOpenResult result = await service.OpenAsync(sourcePath);

            Assert.True(result.Success);
            Assert.True(result.OpenedInternally);
            Assert.Equal(sourcePath, internalOpener.SourcePath);
            Assert.Equal(sourcePath, internalOpener.ContentPath);
            Assert.False(internalOpener.SourceIsEncrypted);
            Assert.IsType<PlainTextTemplate>(internalOpener.ContentTemplate);
            Assert.Null(launcher.OpenedPath);

            service.Dispose();
        }

        [Fact]
        public async Task ShellActivation_EncryptedFileAlwaysRequestsKey()
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var keyRequest = new KeyRequestStub();
            var service = CreateService(
                new SecureFileValidatorStub(),
                keyRequest,
                new InternalFileOpenServiceStub(),
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub());

            WorkspaceFileOpenResult result = await service
                .OpenFromShellAsync(sourcePath);

            Assert.True(result.Success);
            Assert.Equal(0, keyRequest.CallCount);
            Assert.Equal(1, keyRequest.RequestCallCount);
        }

        [Fact]
        public async Task ShellActivation_PlainFileDoesNotRequestKey()
        {
            string sourcePath = Path.Combine(testDirectory, "notes.txt");
            await File.WriteAllTextAsync(sourcePath, "plain");
            var keyRequest = new KeyRequestStub();
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                keyRequest,
                new InternalFileOpenServiceStub(),
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub());

            WorkspaceFileOpenResult result = await service
                .OpenFromShellAsync(sourcePath);

            Assert.True(result.Success);
            Assert.Equal(0, keyRequest.CallCount);
            Assert.Equal(0, keyRequest.RequestCallCount);
        }

        [Theory]
        [InlineData(".png")]
        [InlineData(".mp4")]
        public async Task PlainMedia_IsOpenedInBuiltInViewer(string extension)
        {
            string sourcePath = Path.Combine(testDirectory, "media" + extension);
            string otherPath = Path.Combine(testDirectory, "nested", "other" + extension);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3]);
            var mediaCatalog = new MediaCatalogSelection(
                sourcePath,
                [otherPath, sourcePath]);
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var windowManager = new WindowManagerStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(encrypted: false),
                new SecureFileProcessorStub(extension),
                new KeyRequestStub(),
                windowManager,
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                new DocumentDialogServiceStub());

            WorkspaceFileOpenResult result = await service.OpenAsync(
                sourcePath,
                mediaCatalog);

            Assert.True(result.Success);
            Assert.True(result.OpenedInternally);
            Assert.Equal(typeof(Views.MediaPlayer), windowManager.CreatedWindowType);
            Assert.Equal(Path.GetFullPath(sourcePath), windowManager.CreatedArguments?["path"]);
            Assert.Same(
                mediaCatalog,
                windowManager.CreatedArguments?[MediaCatalogSelection.WindowContextKey]);
            Assert.True(windowManager.CreatedAsSibling);
            Assert.Equal(1, windowManager.ShowCount);
            Assert.Null(launcher.OpenedPath);
            Assert.Equal(0, internalOpener.OpenCount);

            service.Dispose();
        }

        [Fact]
        public async Task LocalProviderPath_IsOpenedInsideCryptoBook()
        {
            string sourcePath = Path.Combine(testDirectory, "provider-path.txt");
            await File.WriteAllTextAsync(sourcePath, "created file");
            var internalOpener = new InternalFileOpenServiceStub();
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                new KeyRequestStub(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub());

            WorkspaceFileOpenResult result = await service.OpenAsync(
                "local://" + sourcePath);

            Assert.True(result.Success);
            Assert.Equal(Path.GetFullPath(sourcePath), internalOpener.SourcePath);
            service.Dispose();
        }

        [Theory]
        [InlineData(".bin")]
        [InlineData(".pdf")]
        public async Task NonDisplayableEncryptedFile_IsOpenedExternally(
            string extension)
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(),
                new SecureFileProcessorStub(extension),
                new KeyRequestStub(),
                new WindowManagerStub(),
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                new DocumentDialogServiceStub());

            WorkspaceFileOpenResult result = await service.OpenAsync(sourcePath);
            string openedPath = Assert.IsType<string>(launcher.OpenedPath);

            Assert.True(result.Success);
            Assert.False(result.OpenedInternally);
            Assert.Equal(extension, Path.GetExtension(openedPath));
            Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(openedPath));
            Assert.Null(internalOpener.DecryptedPath);

            service.Dispose();

            Assert.False(File.Exists(openedPath));
        }

        [Fact]
        public async Task OpenWith_PlainFile_UsesSystemApplicationPicker()
        {
            string sourcePath = Path.Combine(testDirectory, "notes.txt");
            await File.WriteAllTextAsync(sourcePath, "plain");
            var launcher = new FileLauncherStub();
            var keyRequest = new KeyRequestStub();
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                keyRequest,
                new InternalFileOpenServiceStub(),
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                fileLauncher: launcher);

            WorkspaceFileOpenResult result = await service.OpenWithAsync(
                sourcePath);

            Assert.True(result.Success);
            Assert.False(result.OpenedInternally);
            Assert.Equal(Path.GetFullPath(sourcePath), launcher.OpenedPath);
            Assert.Equal(1, launcher.SystemPickerCallCount);
            Assert.Equal(0, keyRequest.CallCount);

            service.Dispose();
        }

        [Fact]
        public async Task OpenWith_EncryptedFile_UsesReadOnlyDecryptedCopy()
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            byte[] encryptedContent = [9, 8, 7];
            await File.WriteAllBytesAsync(sourcePath, encryptedContent);
            var launcher = new FileLauncherStub();
            var service = CreateService(
                new SecureFileValidatorStub(),
                new KeyRequestStub(),
                new InternalFileOpenServiceStub(),
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                new SecureFileProcessorStub(".txt"),
                fileLauncher: launcher);

            WorkspaceFileOpenResult result = await service.OpenWithAsync(
                sourcePath);
            string copyPath = Assert.IsType<string>(launcher.OpenedPath);

            Assert.True(result.Success);
            Assert.False(result.OpenedInternally);
            Assert.Equal(1, launcher.SystemPickerCallCount);
            Assert.NotEqual(sourcePath, copyPath);
            Assert.Equal("secret.txt", Path.GetFileName(copyPath));
            Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(copyPath));
            Assert.True(
                (File.GetAttributes(copyPath) & FileAttributes.ReadOnly) != 0);
            Assert.Throws<UnauthorizedAccessException>(() =>
                File.WriteAllBytes(copyPath, [7, 7, 7]));
            Assert.Equal(
                encryptedContent,
                await File.ReadAllBytesAsync(sourcePath));

            service.Dispose();

            Assert.False(File.Exists(copyPath));
            Assert.Equal(
                encryptedContent,
                await File.ReadAllBytesAsync(sourcePath));
        }

        [Fact]
        public async Task OpenWith_EncryptedFile_KeyCancellationDoesNotLaunch()
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var launcher = new FileLauncherStub();
            var service = CreateService(
                new SecureFileValidatorStub(),
                new KeyRequestStub { Available = false },
                new InternalFileOpenServiceStub(),
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                fileLauncher: launcher);

            WorkspaceFileOpenResult result = await service.OpenWithAsync(
                sourcePath);

            Assert.True(result.Cancelled);
            Assert.Null(launcher.OpenedPath);
            Assert.Equal(0, launcher.SystemPickerCallCount);

            service.Dispose();
        }

        [Fact]
        public async Task EncryptedMedia_IsOpenedInBuiltInViewer()
        {
            string sourcePath = Path.Combine(testDirectory, "image.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var mediaCatalog = new MediaCatalogSelection(
                sourcePath,
                [
                    Path.Combine(testDirectory, "nested", "other.cbook"),
                    sourcePath
                ]);
            var launcher = new FileLauncherStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var windowManager = new WindowManagerStub();
            var service = new WorkspaceFileOpenService(
                new SecureFileValidatorStub(),
                new SecureFileProcessorStub(".png"),
                new KeyRequestStub(),
                windowManager,
                new ProgressDialogServiceStub(),
                launcher,
                CreateTemplateRegistry(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                new DocumentSessionStub(),
                new RecoveryServiceStub(),
                new DocumentDialogServiceStub());

            WorkspaceFileOpenResult result = await service.OpenAsync(
                sourcePath,
                mediaCatalog);
            string playbackPath = Assert.IsType<string>(
                windowManager.CreatedArguments?["path"]);

            Assert.True(result.Success);
            Assert.True(result.OpenedInternally);
            Assert.Equal(typeof(Views.MediaPlayer), windowManager.CreatedWindowType);
            Assert.True(windowManager.CreatedAsSibling);
            Assert.Same(
                mediaCatalog,
                windowManager.CreatedArguments?[MediaCatalogSelection.WindowContextKey]);
            Assert.Equal(1, windowManager.ShowCount);
            Assert.True(File.Exists(playbackPath));
            Assert.Null(launcher.OpenedPath);
            Assert.Null(internalOpener.DecryptedPath);

            service.Dispose();

            Assert.False(File.Exists(playbackPath));
        }

        [Fact]
        public async Task CurrentDocument_IsNotSavedOrReloaded()
        {
            string sourcePath = Path.Combine(testDirectory, "current.txt");
            await File.WriteAllTextAsync(sourcePath, "current");
            var session = new DocumentSessionStub();
            session.Open(sourcePath, new PlainTextTemplate());
            var guard = new UnsavedChangesGuardStub();
            var validator = new SecureFileValidatorStub(encrypted: false);
            var internalOpener = new InternalFileOpenServiceStub();
            var service = CreateService(
                validator,
                new KeyRequestStub(),
                internalOpener,
                guard,
                session,
                new RecoveryServiceStub());

            WorkspaceFileOpenResult result = await service.SwitchAsync(
                sourcePath.ToUpperInvariant());

            Assert.True(result.Success);
            Assert.Equal(0, guard.CallCount);
            Assert.Equal(0, validator.CallCount);
            Assert.Equal(0, internalOpener.OpenCount);
        }

        [Fact]
        public async Task CancelledUnsavedChangesGuard_PreventsProtectedFilePrompt()
        {
            string sourcePath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7]);
            var guard = new UnsavedChangesGuardStub { CanProceed = false };
            var keyRequest = new KeyRequestStub();
            var validator = new SecureFileValidatorStub();
            var service = CreateService(
                validator,
                keyRequest,
                new InternalFileOpenServiceStub(),
                guard,
                new DocumentSessionStub(),
                new RecoveryServiceStub());

            WorkspaceFileOpenResult result = await service.SwitchAsync(sourcePath);

            Assert.True(result.Cancelled);
            Assert.Equal(1, guard.CallCount);
            Assert.Equal(0, validator.CallCount);
            Assert.Equal(0, keyRequest.CallCount);
        }

        [Fact]
        public async Task ProtectedFile_SavesCurrentDocumentBeforeRequestingKey()
        {
            string currentPath = Path.Combine(testDirectory, "current.txt");
            string targetPath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllTextAsync(currentPath, "current");
            await File.WriteAllBytesAsync(targetPath, [9, 8, 7]);
            var order = new List<string>();
            var session = new DocumentSessionStub();
            session.Open(currentPath, new PlainTextTemplate());
            session.IsDirty = true;
            var saver = new CurrentDocumentSaverStub
            {
                Save = () =>
                {
                    order.Add("save");
                    session.IsDirty = false;
                    return true;
                }
            };
            var guard = new UnsavedChangesGuard(
                session,
                saver,
                new DocumentDialogServiceStub
                {
                    SwitchChoice = UnsavedChangesChoice.Save
                });
            var keyRequest = new KeyRequestStub
            {
                OnCall = () => order.Add("key")
            };
            var internalOpener = new InternalFileOpenServiceStub
            {
                OnOpen = (path, template) => session.Open(path, template)
            };
            var service = CreateService(
                new SecureFileValidatorStub(),
                keyRequest,
                internalOpener,
                guard,
                session,
                new RecoveryServiceStub());

            WorkspaceFileOpenResult result = await service.SwitchAsync(targetPath);

            Assert.True(result.Success);
            Assert.Equal(["save", "key"], order);
            Assert.Equal(1, saver.SaveCount);
            Assert.Equal(Path.GetFullPath(targetPath), session.FilePath);
        }

        [Fact]
        public async Task ProtectedFile_KeyCancellation_KeepsCurrentDocument()
        {
            string currentPath = Path.Combine(testDirectory, "current.txt");
            string targetPath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllTextAsync(currentPath, "current");
            await File.WriteAllBytesAsync(targetPath, [9, 8, 7]);
            var session = new DocumentSessionStub();
            session.Open(currentPath, new PlainTextTemplate());
            var recovery = new RecoveryServiceStub();
            var internalOpener = new InternalFileOpenServiceStub();
            var service = CreateService(
                new SecureFileValidatorStub(),
                new KeyRequestStub { Available = false },
                internalOpener,
                new UnsavedChangesGuardStub(),
                session,
                recovery);

            WorkspaceFileOpenResult result = await service.SwitchAsync(targetPath);

            Assert.True(result.Cancelled);
            Assert.Equal(Path.GetFullPath(currentPath), session.FilePath);
            Assert.Equal(0, internalOpener.OpenCount);
            Assert.Equal(0, recovery.DeleteCount);
        }

        [Fact]
        public async Task ProtectedFile_DecryptionFailure_KeepsCurrentDocument()
        {
            string currentPath = Path.Combine(testDirectory, "current.txt");
            string targetPath = Path.Combine(testDirectory, "secret.cbook");
            await File.WriteAllTextAsync(currentPath, "current");
            await File.WriteAllBytesAsync(targetPath, [9, 8, 7]);
            var session = new DocumentSessionStub();
            session.Open(currentPath, new PlainTextTemplate());
            var recovery = new RecoveryServiceStub();
            var expected = new CryptographicException("wrong key");
            var service = CreateService(
                new SecureFileValidatorStub(),
                new KeyRequestStub(),
                new InternalFileOpenServiceStub(),
                new UnsavedChangesGuardStub(),
                session,
                recovery,
                new SecureFileProcessorStub(".txt", expected));

            Exception actual = await Assert.ThrowsAsync<CryptographicException>(
                () => service.SwitchAsync(targetPath));

            Assert.Same(expected, actual);
            Assert.Equal(Path.GetFullPath(currentPath), session.FilePath);
            Assert.Equal(0, recovery.DeleteCount);
        }

        [Fact]
        public async Task InternalOpenFailure_KeepsCurrentSessionAndRecovery()
        {
            string currentPath = Path.Combine(testDirectory, "current.txt");
            string targetPath = Path.Combine(testDirectory, "target.txt");
            await File.WriteAllTextAsync(currentPath, "current");
            await File.WriteAllTextAsync(targetPath, "target");
            var session = new DocumentSessionStub();
            session.Open(currentPath, new PlainTextTemplate());
            var recovery = new RecoveryServiceStub();
            var internalOpener = new InternalFileOpenServiceStub
            {
                Exception = new InvalidDataException("damaged")
            };
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                new KeyRequestStub(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                session,
                recovery);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.SwitchAsync(targetPath));

            Assert.Equal(Path.GetFullPath(currentPath), session.FilePath);
            Assert.Equal(0, recovery.DeleteCount);
        }

        [Fact]
        public async Task SavedCurrentDocument_LoadFailureKeepsSavedDocumentOpen()
        {
            string currentPath = Path.Combine(testDirectory, "current.txt");
            string targetPath = Path.Combine(testDirectory, "target.txt");
            await File.WriteAllTextAsync(currentPath, "current");
            await File.WriteAllTextAsync(targetPath, "target");
            var session = new DocumentSessionStub();
            session.Open(currentPath, new PlainTextTemplate());
            session.IsDirty = true;
            var saver = new CurrentDocumentSaverStub
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
                new DocumentDialogServiceStub
                {
                    SwitchChoice = UnsavedChangesChoice.Save
                });
            var recovery = new RecoveryServiceStub();
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                new KeyRequestStub(),
                new InternalFileOpenServiceStub
                {
                    Exception = new InvalidDataException("damaged")
                },
                guard,
                session,
                recovery);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.SwitchAsync(targetPath));

            Assert.Equal(1, saver.SaveCount);
            Assert.Equal(Path.GetFullPath(currentPath), session.FilePath);
            Assert.False(session.IsDirty);
            Assert.Equal(0, recovery.DeleteCount);
        }

        [Fact]
        public async Task SuccessfulDocumentSwitch_UpdatesRecoveryAfterCommit()
        {
            string targetPath = Path.Combine(testDirectory, "target.txt");
            await File.WriteAllTextAsync(targetPath, "target");
            var session = new DocumentSessionStub();
            var recovery = new RecoveryServiceStub();
            var internalOpener = new InternalFileOpenServiceStub
            {
                OnOpen = (path, template) => session.Open(path, template)
            };
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                new KeyRequestStub(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                session,
                recovery);

            WorkspaceFileOpenResult result = await service.SwitchAsync(targetPath);

            Assert.True(result.Success);
            Assert.Equal(Path.GetFullPath(targetPath), session.FilePath);
            Assert.Equal(1, recovery.DeleteCount);
        }

        [Fact]
        public async Task History_IsUpdatedOnlyAfterSuccessfulOpen()
        {
            string targetPath = Path.Combine(testDirectory, "target.txt");
            await File.WriteAllTextAsync(targetPath, "target");
            var session = new DocumentSessionStub();
            var history = new RecentDocumentServiceStub();
            var internalOpener = new InternalFileOpenServiceStub
            {
                OnOpen = (path, template) => session.Open(path, template)
            };
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                new KeyRequestStub(),
                internalOpener,
                new UnsavedChangesGuardStub(),
                session,
                new RecoveryServiceStub(),
                recentDocumentService: history);

            WorkspaceFileOpenResult result = await service.SwitchAsync(targetPath);
            await service.SwitchAsync(targetPath);
            WorkspaceFileOpenResult failed = await service.SwitchAsync(
                Path.Combine(testDirectory, "missing.txt"));

            Assert.True(result.Success);
            Assert.False(failed.Success);
            Assert.Equal([Path.GetFullPath(targetPath)], history.OpenedPaths);
        }

        [Fact]
        public async Task ConcurrentSwitches_AreSerialized()
        {
            string firstPath = Path.Combine(testDirectory, "first.txt");
            string secondPath = Path.Combine(testDirectory, "second.txt");
            await File.WriteAllTextAsync(firstPath, "first");
            await File.WriteAllTextAsync(secondPath, "second");
            var started = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var session = new DocumentSessionStub();
            var guard = new UnsavedChangesGuardStub();
            var internalOpener = new InternalFileOpenServiceStub
            {
                Started = started,
                WaitFor = release.Task,
                OnOpen = (path, template) => session.Open(path, template)
            };
            var service = CreateService(
                new SecureFileValidatorStub(encrypted: false),
                new KeyRequestStub(),
                internalOpener,
                guard,
                session,
                new RecoveryServiceStub());

            Task<WorkspaceFileOpenResult> first = service.SwitchAsync(firstPath);
            await started.Task;
            Task<WorkspaceFileOpenResult> second = service.SwitchAsync(secondPath);
            await Task.Delay(50);

            Assert.Equal(1, guard.CallCount);
            Assert.Equal(1, internalOpener.OpenCount);

            release.SetResult();
            await Task.WhenAll(first, second);

            Assert.Equal(2, guard.CallCount);
            Assert.Equal(2, internalOpener.OpenCount);
            Assert.Equal(Path.GetFullPath(secondPath), session.FilePath);
        }

        private static WorkspaceFileOpenService CreateService(
            SecureFileValidatorStub validator,
            KeyRequestStub keyRequest,
            InternalFileOpenServiceStub internalOpener,
            IUnsavedChangesGuard guard,
            DocumentSessionStub session,
            RecoveryServiceStub recovery,
            SecureFileProcessorStub? secureFileProcessor = null,
            IRecentDocumentService? recentDocumentService = null,
            FileLauncherStub? fileLauncher = null) =>
            new(
                validator,
                secureFileProcessor ?? new SecureFileProcessorStub(".txt"),
                keyRequest,
                new WindowManagerStub(),
                new ProgressDialogServiceStub(),
                fileLauncher ?? new FileLauncherStub(),
                CreateTemplateRegistry(),
                internalOpener,
                guard,
                session,
                recovery,
                new DocumentDialogServiceStub(),
                keyResetService: null,
                recentDocumentService: recentDocumentService);

        private static IFileTemplateRegistry CreateTemplateRegistry() =>
            new FileTemplateRegistry(
            [
                new PlainTextTemplate(),
                new SecureFileTemplate(),
                new ImageFileTemplate(),
                new VideoFileTemplate(),
                new PdfFileTemplate()
            ]);

        public void Dispose()
        {
            if(Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }

        private sealed class SecureFileValidatorStub(bool encrypted = true):
            ISecureFileValidator
        {
            public int CallCount { get; private set; }

            public Task<bool> HasCryptoBookHeaderAsync(
                string filePath,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(encrypted);
            }
        }

        private sealed class SecureFileProcessorStub(
            string extension,
            Exception? exception = null):
            ISecureFileProcessor
        {
            public Task EncryptFileAsync(
                string inputFile,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task EncryptStreamAsync(
                Stream input,
                string originalExtension,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task DecryptFileAsyncToFile(
                string inputFile,
                string outputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default)
            {
                return exception is null
                    ? File.WriteAllBytesAsync(
                        outputFile + extension,
                        [4, 5, 6],
                        cancellationToken)
                    : Task.FromException(exception);
            }

            public Task<Stream> DecryptFileAsyncToStream(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<DecryptedFileContent> DecryptFileContentAsync(
                string inputFile,
                IProgressReporter? progress = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new DecryptedFileContent(
                    new MemoryStream([4, 5, 6]),
                    extension));
        }

        private sealed class InternalFileOpenServiceStub:
            IWorkspaceInternalFileOpenService
        {
            public Exception? Exception { get; init; }
            public Action<string, IFileTemplate>? OnOpen { get; init; }
            public TaskCompletionSource? Started { get; init; }
            public Task? WaitFor { get; init; }
            public int OpenCount { get; private set; }
            public string? SourcePath { get; private set; }
            public string? ContentPath { get; private set; }
            public string? EncryptedPath => SourcePath;
            public string? DecryptedPath => ContentPath;
            public IFileTemplate? ContentTemplate { get; private set; }
            public bool SourceIsEncrypted { get; private set; }

            public async Task OpenDocumentAsync(
                string sourcePath,
                string contentPath,
                IFileTemplate contentTemplate,
                bool sourceIsEncrypted,
                CancellationToken cancellationToken = default)
            {
                OpenCount++;
                if(Exception is not null)
                    throw Exception;
                Started?.TrySetResult();
                if(WaitFor is not null)
                    await WaitFor.WaitAsync(cancellationToken);
                SourcePath = sourcePath;
                ContentPath = contentPath;
                ContentTemplate = contentTemplate;
                SourceIsEncrypted = sourceIsEncrypted;
                OnOpen?.Invoke(sourcePath, contentTemplate);
            }
        }

        private sealed class KeyRequestStub: IEncryptionKeyRequestService
        {
            public Action? OnCall { get; init; }
            public bool Available { get; init; } = true;
            public int CallCount { get; private set; }
            public int RequestCallCount { get; private set; }

            public bool EnsureKeyAvailable()
            {
                CallCount++;
                OnCall?.Invoke();
                return Available;
            }

            public bool RequestKey()
            {
                RequestCallCount++;
                OnCall?.Invoke();
                return Available;
            }
        }

        private sealed class ProgressDialogServiceStub: IProgressDialogService
        {
            public Task<T> RunAsync<T>(
                string operationName,
                Func<IProgressReporter, CancellationToken, Task<T>> operation) =>
                operation(new ProgressReporterStub(), CancellationToken.None);
        }

        private sealed class ProgressReporterStub: IProgressReporter
        {
            public void Report(double? value, string? currentInfo = null)
            {
            }
        }

        private sealed class UnsavedChangesGuardStub: IUnsavedChangesGuard
        {
            public bool CanProceed { get; init; } = true;
            public Action? OnCall { get; init; }
            public int CallCount { get; private set; }

            public Task<bool> CanProceedAsync(
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                OnCall?.Invoke();
                return Task.FromResult(CanProceed);
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
            public bool HasDocument => FilePath is not null;

            public void Open(string filePath, IFileTemplate template)
            {
                FilePath = filePath;
                Template = template;
            }

            public void Open(
                string filePath,
                IFileTemplate template,
                System.Windows.Documents.FlowDocument document) =>
                Open(filePath, template);

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

        private sealed class RecoveryServiceStub: IDocumentRecoveryService
        {
            public bool HasSnapshot => false;
            public int DeleteCount { get; private set; }
            public void Start()
            {
            }
            public Task StopAsync() => Task.CompletedTask;
            public Task<bool> RestoreSnapshotAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult(false);
            public Task DeleteSnapshotAsync()
            {
                DeleteCount++;
                return Task.CompletedTask;
            }
            public void Dispose()
            {
            }
        }

        private sealed class RecentDocumentServiceStub: IRecentDocumentService
        {
            public event EventHandler? Changed
            {
                add { }
                remove { }
            }

            public IReadOnlyList<RecentDocument> Items => [];
            public List<string> OpenedPaths { get; } = [];

            public Task InitializeAsync(
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task RecordOpenedAsync(
                string path,
                CancellationToken cancellationToken = default)
            {
                OpenedPaths.Add(path);
                return Task.CompletedTask;
            }

            public Task RecordSavedAsync(
                string path,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task UpdatePathAsync(
                string oldPath,
                string newPath,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task RemoveAsync(
                string path,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class DocumentDialogServiceStub: IDocumentDialogService
        {
            public UnsavedChangesChoice SwitchChoice { get; init; } =
                UnsavedChangesChoice.Cancel;

            public bool ConfirmRecovery() => false;
            public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() =>
                UnsavedChangesChoice.Cancel;
            public UnsavedChangesChoice ConfirmSwitchWithUnsavedChanges() =>
                SwitchChoice;
            public void ShowRecoveryError(Exception exception)
            {
            }
            public void ShowRecoveryCleanupError(Exception exception)
            {
            }
        }

        private sealed class CurrentDocumentSaverStub: ICurrentDocumentSaver
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

        private sealed class FileLauncherStub: IFileLauncherService
        {
            public string? OpenedPath { get; private set; }
            public string? OpenedVerb { get; private set; }
            public int SystemPickerCallCount { get; private set; }

            public LaunchResult Open(string target)
            {
                OpenedPath = target;
                return LaunchResult.Ok("open", target);
            }

            public LaunchResult Open(string target, string verb)
            {
                OpenedPath = target;
                OpenedVerb = verb;
                return LaunchResult.Ok($"shell:{verb}", target);
            }
            public LaunchResult ShellExecute(ShellLaunchOptions options) =>
                throw new NotSupportedException();
            public LaunchResult OpenWith(
                string applicationPath,
                string target,
                string? arguments = null,
                string? workingDirectory = null) =>
                throw new NotSupportedException();
            public LaunchResult ShowOpenWithDialog(string target)
            {
                OpenedPath = target;
                SystemPickerCallCount++;
                return LaunchResult.Ok("shell:open-with-dialog", target);
            }
            public LaunchResult StartProcess(ProcessLaunchOptions options) =>
                throw new NotSupportedException();
            public LaunchResult RevealInExplorer(string path, bool select = true) =>
                throw new NotSupportedException();
            public LaunchResult Print(string path) =>
                throw new NotSupportedException();
            public LaunchResult Edit(string path) =>
                throw new NotSupportedException();
            public LaunchResult RunAsAdmin(
                string path,
                string? arguments = null) =>
                throw new NotSupportedException();
            public LaunchResult RunCmd(
                string command,
                string? workingDirectory = null,
                bool runAsAdmin = false) =>
                throw new NotSupportedException();
            public LaunchResult RunPowerShell(
                string command,
                string? workingDirectory = null,
                bool runAsAdmin = false) =>
                throw new NotSupportedException();
        }

        private sealed class WindowManagerStub: IWindowManager
        {
            public Type? CreatedWindowType { get; private set; }
            public IReadOnlyDictionary<string, object?>? CreatedArguments { get; private set; }
            public bool CreatedAsSibling { get; private set; }
            public int ShowCount { get; private set; }

            public Guid CreateWindow<T>(
                IReadOnlyDictionary<string, object?>? args = null)
                where T: Window
            {
                CreatedWindowType = typeof(T);
                CreatedArguments = args;
                return Guid.NewGuid();
            }
            public Guid CreateSiblingWindow<T>(
                IReadOnlyDictionary<string, object?>? args = null)
                where T: Window
            {
                CreatedAsSibling = true;
                return CreateWindow<T>(args);
            }
            public TResult? GetResult<TResult>(Guid guid) => default;
            public void ShowWindow(Guid windowId)
            {
                ShowCount++;
            }
            public void ShowWindowDialog(Guid windowId)
            {
            }
            public void ActivateWindow(Guid windowId)
            {
            }
            public void CloseWindow(Guid windowId)
            {
            }
            public bool IsWindowOpen(Guid windowId) => false;
            public WindowHost? FindHostWindow(Guid windowId) => null;
        }
    }
}
