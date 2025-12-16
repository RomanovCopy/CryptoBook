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
        private readonly IFileTemplateRegistry _fileTemplate;


        public FilePickerService(IFileManagerService fileManagerService, IFileTemplateRegistry fileTemplate)
        {
            _fs = fileManagerService ?? throw new ArgumentNullException(nameof(fileManagerService));
            _fileTemplate = fileTemplate ?? throw new ArgumentNullException(nameof(fileTemplate));
        }

        public Task<string?> PickFileAsync(string? initialDirectory, CancellationToken ct)
        {
            // допускаем "сырой" локальный путь или уже "local://..."
            string start = string.IsNullOrWhiteSpace(initialDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : _fs.NormalizePath(initialDirectory);

            // отрезаем схему для OpenFileDialog
            string native = start.Contains("://")
                ? start[(start.IndexOf("://") + 3)..]
                : start;

            

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите файл",
                Filter = GetFilterString()?? "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FilterIndex = 1,
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultDirectory=native,
            };

            bool? result = dialog.ShowDialog();

            if(result == true)
            {
                string filePath = dialog.FileName;
                return Task.FromResult<string?>(_fs.NormalizePath(filePath));
            }

            return Task.FromResult<string?>(null);
        }


        private string GetFilterString()
        {
            var templates = _fileTemplate.GetAll();
            var filterParts = new List<string>();
            foreach(var template in templates)
            {
                string extensions = string.Join(";", template.DefaultExtension.Select(ext => $"*{ext}"));
                filterParts.Add($"{template.DisplayName} ({extensions})|{extensions}");
            }
            filterParts.Add("All Files (*.*)|*.*");
            return string.Join("|", filterParts);
        }

    }
}
