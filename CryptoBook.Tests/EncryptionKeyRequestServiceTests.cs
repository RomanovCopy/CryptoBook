using CryptoBook.DTO;
using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;

using System.Windows;

using Xunit;

namespace CryptoBook.Tests;

public sealed class EncryptionKeyRequestServiceTests
{
    [Fact]
    public void RequestKey_ShowsDialogWhenKeyIsAlreadyAvailable()
    {
        var keyProvider = new KeyProviderStub { HasKey = true };
        var windowManager = new WindowManagerStub { DialogResult = true };
        var service = new EncryptionKeyRequestService(
            keyProvider,
            windowManager);

        Assert.True(service.EnsureKeyAvailable());
        Assert.Equal(0, windowManager.DialogCount);

        Assert.True(service.RequestKey());
        Assert.Equal(1, windowManager.DialogCount);
        Assert.Equal(typeof(Views.KeyInputWindow), windowManager.CreatedType);
    }

    [Fact]
    public void RequestKey_ReturnsFalseWhenDialogIsCancelledWithCachedKey()
    {
        var keyProvider = new KeyProviderStub { HasKey = true };
        var windowManager = new WindowManagerStub { DialogResult = false };
        var service = new EncryptionKeyRequestService(
            keyProvider,
            windowManager);

        Assert.False(service.RequestKey());
        Assert.True(keyProvider.HasKey);
    }

    private sealed class KeyProviderStub: IKeyProvider
    {
        public bool HasKey { get; set; }

        public void SetKey(ReadOnlySpan<char> password) => HasKey = true;

        public byte[] DeriveKey(byte[] salt) =>
            throw new NotSupportedException();

        public Task<byte[]> DeriveKeyAsync(
            ReadOnlyMemory<byte> salt,
            KeyDerivationParameters parameters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Clear() => HasKey = false;
    }

    private sealed class WindowManagerStub: IWindowManager
    {
        public Type? CreatedType { get; private set; }
        public int DialogCount { get; private set; }
        public bool DialogResult { get; init; }

        public Guid CreateWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T: Window
        {
            CreatedType = typeof(T);
            return Guid.NewGuid();
        }

        public TResult? GetResult<TResult>(Guid guid) =>
            DialogResult is TResult result ? result : default;
        public void ShowWindow(Guid windowId) { }
        public void ShowWindowDialog(Guid windowId) => DialogCount++;
        public void ActivateWindow(Guid windowId) { }
        public void CloseWindow(Guid windowId) { }
        public bool IsWindowOpen(Guid windowId) => false;
        public WindowHost? FindHostWindow(Guid windowId) => null;
    }
}
