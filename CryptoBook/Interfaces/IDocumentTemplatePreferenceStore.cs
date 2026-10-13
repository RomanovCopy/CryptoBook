namespace CryptoBook.Interfaces;

public interface IDocumentTemplatePreferenceStore
{
    string Load();
    void Save(string templateId);
}
