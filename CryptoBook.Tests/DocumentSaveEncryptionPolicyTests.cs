using CryptoBook.DTO;
using CryptoBook.FileTemplates;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using Xunit;

namespace CryptoBook.Tests
{
    public sealed class DocumentSaveEncryptionPolicyTests
    {
        [Fact]
        public async Task PlaintextFileWithKey_WhenAccepted_UsesSecureTemplate()
        {
            var messages = new MessageServiceStub(true);
            var policy = CreatePolicy(messages, hasKey: true);
            var target = new DocumentSaveTarget(
                "document.XamlPackage",
                new XamlPackageFileTemplate());

            DocumentSaveTarget? resolved = await policy.ResolveAsync(
                target,
                sourceIsPlaintextFile: true);

            Assert.NotNull(resolved);
            Assert.IsType<SecureFileTemplate>(resolved.Template);
            Assert.Equal(target.FilePath, resolved.FilePath);
            Assert.Single(messages.Messages);
            Assert.True(messages.Messages[0].IsCanceled);
        }

        [Fact]
        public async Task PlaintextFileWithKey_WhenDeclined_RemainsPlaintext()
        {
            var messages = new MessageServiceStub(false);
            var policy = CreatePolicy(messages, hasKey: true);
            var target = new DocumentSaveTarget(
                "document.XamlPackage",
                new XamlPackageFileTemplate());

            DocumentSaveTarget? resolved = await policy.ResolveAsync(
                target,
                sourceIsPlaintextFile: true);

            Assert.Same(target, resolved);
            Assert.Single(messages.Messages);
        }

        [Fact]
        public async Task PlaintextFileWithoutKey_IsSavedWithoutQuestion()
        {
            var messages = new MessageServiceStub(true);
            var policy = CreatePolicy(messages, hasKey: false);
            var target = new DocumentSaveTarget(
                "document.XamlPackage",
                new XamlPackageFileTemplate());

            DocumentSaveTarget? resolved = await policy.ResolveAsync(
                target,
                sourceIsPlaintextFile: true);

            Assert.Same(target, resolved);
            Assert.Empty(messages.Messages);
        }

        [Fact]
        public async Task EncryptedTarget_RequiresConfirmationOnEverySave()
        {
            var messages = new MessageServiceStub(true, false);
            var policy = CreatePolicy(messages, hasKey: true);
            var target = new DocumentSaveTarget(
                "document.cbook",
                new SecureFileTemplate());

            DocumentSaveTarget? first = await policy.ResolveAsync(
                target,
                sourceIsPlaintextFile: false);
            DocumentSaveTarget? second = await policy.ResolveAsync(
                target,
                sourceIsPlaintextFile: false);

            Assert.Same(target, first);
            Assert.Null(second);
            Assert.Equal(2, messages.Messages.Count);
        }

        private static DocumentSaveEncryptionPolicy CreatePolicy(
            MessageServiceStub messages,
            bool hasKey)
        {
            var keyProvider = new KeyProviderStub(hasKey);
            var secureTemplate = new SecureFileTemplate();
            var registry = new FileTemplateRegistry(
                [new XamlPackageFileTemplate(), secureTemplate]);
            return new DocumentSaveEncryptionPolicy(
                messages,
                keyProvider,
                registry);
        }

        private sealed class MessageServiceStub: IMessageService
        {
            private readonly Queue<bool> results;
            private readonly Dictionary<Guid, bool> resultsById = [];

            public MessageServiceStub(params bool[] results)
            {
                this.results = new Queue<bool>(results);
            }

            public List<(string Title, string Message, bool IsCanceled)> Messages
                { get; } = [];

            public Task<Guid> ShowMessage(
                string title,
                string message,
                bool isCanceled = false)
            {
                Guid id = Guid.NewGuid();
                Messages.Add((title, message, isCanceled));
                resultsById[id] = results.Dequeue();
                return Task.FromResult(id);
            }

            public void CloseDialog(Guid id)
            {
            }

            public bool ShowConfirmation(Guid id) => resultsById[id];
        }

        private sealed class KeyProviderStub: IKeyProvider
        {
            public KeyProviderStub(bool hasKey)
            {
                HasKey = hasKey;
            }

            public bool HasKey { get; }

            public void SetKey(ReadOnlySpan<char> password) =>
                throw new NotSupportedException();

            public byte[] DeriveKey(byte[] salt) =>
                throw new NotSupportedException();

            public Task<byte[]> DeriveKeyAsync(
                ReadOnlyMemory<byte> salt,
                KeyDerivationParameters parameters,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public void Clear()
            {
            }
        }
    }
}
