using CryptoBook.Interfaces;

using System.Diagnostics;

namespace CryptoBook.Services
{
    public sealed class UriNavigationService: IUriNavigationService
    {
        public bool TryOpen(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            if(!uri.IsAbsoluteUri ||
               (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != Uri.UriSchemeMailto))
            {
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
                {
                    UseShellExecute = true
                });
                return true;
            } catch
            {
                return false;
            }
        }
    }
}
