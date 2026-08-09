using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class UserDocumentLineSpacingPreferenceStore:
        IDocumentLineSpacingPreferenceStore
    {
        public double Load() => DocumentLineSpacing.NormalizeRatio(
            Properties.Settings.Default.DocumentLineSpacingRatio);

        public void Save(double ratio)
        {
            Properties.Settings.Default.DocumentLineSpacingRatio =
                DocumentLineSpacing.NormalizeRatio(ratio);
            Properties.Settings.Default.Save();
        }
    }
}
