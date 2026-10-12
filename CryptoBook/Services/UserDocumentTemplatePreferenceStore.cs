using CryptoBook.Interfaces;

namespace CryptoBook.Services;

public sealed class UserDocumentTemplatePreferenceStore: IDocumentTemplatePreferenceStore
{
    public string Load() => Properties.Settings.Default.DefaultDocumentTemplateId;

    public void Save(string templateId)
    {
        Properties.Settings.Default.DefaultDocumentTemplateId = templateId;
        Properties.Settings.Default.Save();
    }
}
