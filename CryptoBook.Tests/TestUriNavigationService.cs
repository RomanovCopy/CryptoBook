using CryptoBook.Interfaces;

namespace CryptoBook.Tests;

internal sealed class TestUriNavigationService: IUriNavigationService
{
    public Uri? LastOpenedUri { get; private set; }

    public bool TryOpen(Uri uri)
    {
        LastOpenedUri = uri;
        return true;
    }
}
