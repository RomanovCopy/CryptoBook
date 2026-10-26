using CryptoBook.Interfaces;
using System.Runtime.InteropServices;

namespace CryptoBook.Services;

public sealed class TextClipboardService : ITextClipboardService
{
    public bool TrySetText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch(ExternalException) { return false; }
    }
}
