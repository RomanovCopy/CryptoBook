using System.Windows.Media.Imaging;

namespace CryptoBook.DTO;

public sealed record ImageEditOptions(int Width, int Height,
    double Contrast = 0, double Brightness = 0, double Saturation = 0);

public sealed record ImageEditRequest(BitmapSource Source, string SourcePath, bool IsEncrypted);
