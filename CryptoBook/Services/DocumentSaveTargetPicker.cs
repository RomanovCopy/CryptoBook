using CryptoBook.DTO;
using CryptoBook.Interfaces;

using CryptoBook.Infrastructure;

using System.IO;

using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace CryptoBook.Services
{
    public sealed class DocumentSaveTargetPicker:
        IDocumentSaveTargetPicker
    {
        private readonly IReadOnlyList<IFileTemplate> templates;

        public DocumentSaveTargetPicker(
            IFileTemplateRegistry templateRegistry,
            IDocumentFormatHandlerRegistry formatHandlers)
        {
            ArgumentNullException.ThrowIfNull(templateRegistry);
            ArgumentNullException.ThrowIfNull(formatHandlers);

            templates = templateRegistry
                .GetAll()
                .Where(template =>
                    template.OpenMode == FileOpenMode.Document &&
                    formatHandlers.Find(template) is not null)
                .OrderBy(template => template.DisplayName)
                .ToArray();
        }

        public DocumentSaveTarget? Pick(
            string? currentFilePath,
            IFileTemplate? currentTemplate)
        {
            if(templates.Count == 0)
                throw new InvalidOperationException(
                LocalizationManager.GetString("Document.NoSaveFormats"));

            int selectedIndex = FindTemplateIndex(currentTemplate);
            var dialog = new WpfSaveFileDialog
            {
                Title = LocalizationManager.GetString(
                    "Document.SaveAsTitle"),
                AddExtension = true,
                OverwritePrompt = true,
                CheckPathExists = true,
                Filter = string.Join(
                    "|",
                    templates.Select(CreateFilter)),
                FilterIndex = selectedIndex + 1,
                DefaultExt = templates[selectedIndex].DefaultExtension,
                FileName = string.IsNullOrWhiteSpace(currentFilePath)
                    ? templates[selectedIndex].SuggestedBaseName
                    : Path.GetFileName(currentFilePath),
                InitialDirectory = string.IsNullOrWhiteSpace(
                    currentFilePath)
                    ? null
                    : Path.GetDirectoryName(currentFilePath)
            };

            if(dialog.ShowDialog() != true)
                return null;

            IFileTemplate selectedTemplate =
                FindByExtension(Path.GetExtension(dialog.FileName))
                ?? templates[Math.Clamp(
                    dialog.FilterIndex - 1,
                    0,
                    templates.Count - 1)];
            return new DocumentSaveTarget(
                dialog.FileName,
                selectedTemplate);
        }

        private int FindTemplateIndex(IFileTemplate? currentTemplate)
        {
            if(currentTemplate is null)
                return FindPreferredTemplateIndex();

            for(int index = 0; index < templates.Count; index++)
            {
                if(string.Equals(
                    templates[index].Id,
                    currentTemplate.Id,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return FindPreferredTemplateIndex();
        }

        private int FindPreferredTemplateIndex()
        {
            for(int index = 0; index < templates.Count; index++)
            {
                if(string.Equals(
                    templates[index].DefaultExtension,
                    ".XamlPackage",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }

        private IFileTemplate? FindByExtension(string extension) =>
            templates.FirstOrDefault(template =>
                template.CanHandleExtension(extension));

        private static string CreateFilter(IFileTemplate template)
        {
            string patterns = string.Join(
                ";",
                template.Extensions.Select(extension =>
                    $"*{NormalizeExtension(extension)}"));
            return $"{template.DisplayName} ({patterns})|{patterns}";
        }

        private static string NormalizeExtension(string extension) =>
            extension.StartsWith('.')
                ? extension
                : $".{extension}";
    }
}
