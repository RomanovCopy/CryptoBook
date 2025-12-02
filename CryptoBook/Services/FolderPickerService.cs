using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public sealed class FolderPickerService: IFolderPickerService
    {
        private readonly IFileManagerService _fs;

        public FolderPickerService(IFileManagerService fs) => _fs = fs;

        public Task<string?> PickFolderAsync(string? initialDirectory, CancellationToken ct)
        {
            // допускаем "сырой" локальный путь или уже "local://..."
            string start = string.IsNullOrWhiteSpace(initialDirectory)
                ? "local://C:/"
                : _fs.NormalizePath(initialDirectory);

            // отрезаем схему для FolderBrowserDialog
            string native = start.Contains("://")
                ? start[(start.IndexOf("://") + 3)..]
                : start;

            using var dlg = new FolderBrowserDialog
            {
                SelectedPath = native,
                ShowNewFolderButton = false,
                Description = "Select a folder for the new file"
            };

            var result = dlg.ShowDialog();
            if(result == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                return Task.FromResult<string?>(_fs.NormalizePath(dlg.SelectedPath));
            }

            return Task.FromResult<string?>(null);
        }
    }
}
