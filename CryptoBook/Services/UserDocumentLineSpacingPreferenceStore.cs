using CryptoBook.Interfaces;

namespace CryptoBook.Services
{
    public sealed class UserDocumentLineSpacingPreferenceStore:
        IDocumentLineSpacingPreferenceStore
    {
        private readonly IDocumentLineSpacingService lineSpacingService;

        public UserDocumentLineSpacingPreferenceStore(
            IDocumentLineSpacingService lineSpacingService) =>
            this.lineSpacingService = lineSpacingService ??
                throw new ArgumentNullException(nameof(lineSpacingService));

        public double Load() => lineSpacingService.Normalize(
            Properties.Settings.Default.DocumentLineSpacingRatio);

        public void Save(double ratio)
        {
            Properties.Settings.Default.DocumentLineSpacingRatio =
                lineSpacingService.Normalize(ratio);
            Properties.Settings.Default.Save();
        }
    }
}
