namespace CryptoBook.Interfaces
{
    public interface IDocumentLineSpacingPreferenceStore: IService
    {
        double Load();
        void Save(double ratio);
    }
}
