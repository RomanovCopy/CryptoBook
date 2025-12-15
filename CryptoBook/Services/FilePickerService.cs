using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CryptoBook.Services
{
    public sealed class FilePickerService:IFilePickerService
    {
        private readonly IFileManagerService _fs;
        public FilePickerService(IFileManagerService fileManagerService)
        {
            _fs = fileManagerService??throw new ArgumentNullException(nameof(fileManagerService));
        }

        public Task<string?> PickFileAsync(string? initialDirectory, CancellationToken ct)
        {
            // допускаем "сырой" локальный путь или уже "local://..."
            string start = string.IsNullOrWhiteSpace(initialDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : _fs.NormalizePath(initialDirectory);

            // отрезаем схему для FolderBrowserDialog
            string native = start.Contains("://")
                ? start[(start.IndexOf("://") + 3)..]
                : start;

            using var dlg = new OpenFileDialog()
            {
                InitialDirectory = initialDirectory,

            };

            //using var dlg = new FolderBrowserDialog
            //{
            //    SelectedPath = native,
            //    ShowNewFolderButton = true,
            //    Description = "Select a folder",
            //};

            //var result = dlg.ShowDialog();
            //if(result == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            //{
            //    return Task.FromResult<string?>(_fs.NormalizePath(dlg.SelectedPath));
            //}

            return Task.FromResult<string?>(null);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
