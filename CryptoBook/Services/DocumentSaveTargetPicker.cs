using CryptoBook.DTO;
using CryptoBook.FileTemplates;
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
        private readonly IMarkdownDocumentState? markdownDocument;

        public DocumentSaveTargetPicker(
            IFileTemplateRegistry templateRegistry,
            IDocumentFormatHandlerRegistry formatHandlers,
            IMarkdownDocumentState? markdownDocument = null)
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
            this.markdownDocument = markdownDocument;
        }

        public DocumentSaveTarget? Pick(
            string? currentFilePath,
            IFileTemplate? currentTemplate)
        {
            IReadOnlyList<IFileTemplate> availableTemplates =
                GetAvailableTemplates();
            if(availableTemplates.Count == 0)
                throw new InvalidOperationException(
                LocalizationManager.GetString("Document.NoSaveFormats"));

            int selectedIndex = FindTemplateIndex(
                availableTemplates,
                currentTemplate);
            var dialog = new WpfSaveFileDialog
            {
                Title = LocalizationManager.GetString(
                    "Document.SaveAsTitle"),
                AddExtension = true,
                OverwritePrompt = true,
                CheckPathExists = true,
                Filter = string.Join(
                    "|",
                    availableTemplates.Select(CreateFilter)),
                FilterIndex = selectedIndex + 1,
                DefaultExt = availableTemplates[selectedIndex].DefaultExtension,
                FileName = string.IsNullOrWhiteSpace(currentFilePath)
                    ? availableTemplates[selectedIndex].SuggestedBaseName
                    : Path.GetFileName(currentFilePath),
                InitialDirectory = string.IsNullOrWhiteSpace(
                    currentFilePath)
                    ? null
                    : Path.GetDirectoryName(currentFilePath)
            };

            if(dialog.ShowDialog() != true)
                return null;

            string selectedExtension = Path.GetExtension(dialog.FileName);
            IFileTemplate? extensionTemplate = FindByExtension(
                availableTemplates,
                selectedExtension);
            bool isMarkdownExtension = templates
                .OfType<MarkdownFileTemplate>()
                .Cast<IFileTemplate>()
                .Any(template => template.CanHandleExtension(
                    selectedExtension));
            if((markdownDocument?.IsActive == true &&
                extensionTemplate is null) ||
               (markdownDocument?.IsActive != true &&
                isMarkdownExtension))
            {
                throw new InvalidOperationException(
                    LocalizationManager.GetString(
                        "Document.MarkdownRequiresSourceText"));
            }

            IFileTemplate selectedTemplate = extensionTemplate
                ?? availableTemplates[Math.Clamp(
                    dialog.FilterIndex - 1,
                    0,
                    availableTemplates.Count - 1)];
            return new DocumentSaveTarget(
                dialog.FileName,
                selectedTemplate);
        }

        private int FindTemplateIndex(
            IReadOnlyList<IFileTemplate> availableTemplates,
            IFileTemplate? currentTemplate)
        {
            if(currentTemplate is null)
                return FindPreferredTemplateIndex(availableTemplates);

            for(int index = 0; index < availableTemplates.Count; index++)
            {
                if(string.Equals(
                    availableTemplates[index].Id,
                    currentTemplate.Id,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return FindPreferredTemplateIndex(availableTemplates);
        }

        private static int FindPreferredTemplateIndex(
            IReadOnlyList<IFileTemplate> availableTemplates)
        {
            for(int index = 0; index < availableTemplates.Count; index++)
            {
                if(string.Equals(
                    availableTemplates[index].DefaultExtension,
                    ".XamlPackage",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }

        private static IFileTemplate? FindByExtension(
            IReadOnlyList<IFileTemplate> availableTemplates,
            string extension) =>
            availableTemplates.FirstOrDefault(template =>
                template.CanHandleExtension(extension));

        private IReadOnlyList<IFileTemplate> GetAvailableTemplates()
        {
            if(markdownDocument?.IsActive == true)
            {
                return templates.Where(template =>
                    template is MarkdownFileTemplate or PlainTextTemplate)
                    .ToArray();
            }

            // Entering Markdown must start with source text. A formatted
            // FlowDocument is intentionally never reverse-converted to it.
            return templates.Where(template =>
                template is not MarkdownFileTemplate).ToArray();
        }

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
