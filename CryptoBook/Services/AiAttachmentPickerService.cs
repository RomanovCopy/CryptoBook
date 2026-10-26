using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.Services;

public sealed class AiAttachmentPickerService : IAiAttachmentPicker
{
    public Task<IReadOnlyList<string>> PickAttachmentsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationManager.GetString("AI.AttachFiles"),
            Filter = LocalizationManager.GetString("AI.AttachmentFilter"),
            Multiselect = true,
            CheckFileExists = true
        };
        IReadOnlyList<string> paths = picker.ShowDialog() == true ? picker.FileNames : [];
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(paths);
    }
}
