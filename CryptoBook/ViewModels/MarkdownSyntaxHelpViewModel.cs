using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class MarkdownSyntaxHelpViewModel:
        ViewModelBase,
        IMarkdownSyntaxHelpViewModel
    {
        private readonly IPageNavigationService navigationService;
        private IReadOnlyList<MarkdownSyntaxHelpGroup> groups;

        public MarkdownSyntaxHelpViewModel(
            IPageNavigationService navigationService)
        {
            this.navigationService = navigationService ??
                throw new ArgumentNullException(nameof(navigationService));
            groups = CreateGroups();
            LocalizationManager.CultureChanged += OnCultureChanged;
        }

        public IReadOnlyList<MarkdownSyntaxHelpGroup> Groups => groups;

        public ICommand BackToEditor => backToEditor ??=
            new RelayCommand(_ => navigationService.Navigate("MarkdownEditor"));
        private RelayCommand? backToEditor;

        public ICommand PageLoaded => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand PageClear => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand Loaded => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand Close => BackToEditor;
        public ICommand Closing => noOperation ??=
            new RelayCommand(_ => { });
        public ICommand Closed => closed ??= new RelayCommand(_ =>
        {
            LocalizationManager.CultureChanged -= OnCultureChanged;
        });
        private RelayCommand? noOperation;
        private RelayCommand? closed;

        private static IReadOnlyList<MarkdownSyntaxHelpGroup> CreateGroups() =>
        [
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Editor"),
                [
                    Entry("Ctrl+S", "MarkdownHelp.Editor.Save.Result"),
                    Entry("Ctrl+Shift+S", "MarkdownHelp.Editor.SaveAs.Result"),
                    Entry(
                        $"{LocalizationManager.GetString("Editor.Preview")} / " +
                        LocalizationManager.GetString("Editor.Editor"),
                        "MarkdownHelp.Editor.Toggle.Result"),
                    Entry("Tab", "MarkdownHelp.Editor.Tab.Result"),
                    Entry("? / Esc", "MarkdownHelp.Editor.Help.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Structure"),
                [
                    Entry(
                        "# H1\n## H2\n…\n###### H6",
                        "MarkdownHelp.Headings.Result"),
                    Entry(
                        "Heading 1\n=========",
                        "MarkdownHelp.SetextH1.Result"),
                    Entry(
                        "Heading 2\n---------",
                        "MarkdownHelp.SetextH2.Result"),
                    Entry("---\n***\n___", "MarkdownHelp.Rule.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Lists"),
                [
                    Entry("> quote\n>> nested", "MarkdownHelp.Quote.Result"),
                    Entry("- item\n+ item\n* item", "MarkdownHelp.BulletedList.Result"),
                    Entry("1. item\n2) item", "MarkdownHelp.NumberedList.Result"),
                    Entry("- parent\n    - child", "MarkdownHelp.NestedList.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Inline"),
                [
                    Entry("*italic*\n_italic_", "MarkdownHelp.Italic.Result"),
                    Entry("**bold**\n__bold__", "MarkdownHelp.Bold.Result"),
                    Entry("***bold italic***", "MarkdownHelp.BoldItalic.Result"),
                    Entry("~~strike~~", "MarkdownHelp.Strikethrough.Result"),
                    Entry("`code`\n``code with ` inside``", "MarkdownHelp.InlineCode.Result"),
                    Entry("\\*literal asterisks\\*", "MarkdownHelp.Escape.Result"),
                    Entry("&copy;\n&#169;", "MarkdownHelp.Entity.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Spacing"),
                [
                    Entry("text\n\ntext", "MarkdownHelp.Paragraph.Result"),
                    Entry("text\ntext", "MarkdownHelp.SoftBreak.Result"),
                    Entry("text  \ntext", "MarkdownHelp.LineBreak.Result"),
                    Entry("text\\\ntext", "MarkdownHelp.BackslashBreak.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Links"),
                [
                    Entry("[text](https://example.com)", "MarkdownHelp.Link.Result"),
                    Entry(
                        "[text][ref]\n\n[ref]: https://example.com",
                        "MarkdownHelp.ReferenceLink.Result"),
                    Entry("<https://example.com>", "MarkdownHelp.AutoLink.Result"),
                    Entry("<name@example.com>", "MarkdownHelp.AutoEmail.Result"),
                    Entry("https://example.com", "MarkdownHelp.BareLink.Result"),
                    Entry("![alt](image.png)", "MarkdownHelp.Image.Result"),
                    Entry(
                        "![alt][image]\n\n[image]: images/picture.png",
                        "MarkdownHelp.ReferenceImage.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Code"),
                [
                    Entry("```csharp\ncode\n```", "MarkdownHelp.CodeBlock.Result"),
                    Entry("~~~text\ncode\n~~~", "MarkdownHelp.TildeCode.Result"),
                    Entry("    indented code", "MarkdownHelp.IndentedCode.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Tables"),
                [
                    Entry(
                        "| A | B |\n|---|---|\n| 1 | 2 |",
                        "MarkdownHelp.Table.Result"),
                    Entry(
                        "+---+---+\n| A | B |\n+===+===+\n| 1 | 2 |\n+---+---+",
                        "MarkdownHelp.GridTable.Result")
                ]),
            new(
                LocalizationManager.GetString("MarkdownHelp.Group.Limitations"),
                [
                    Entry("<b>HTML</b>", "MarkdownHelp.Html.Result"),
                    Entry(
                        "[file](file:///…)\n[ftp](ftp://…)\n[relative](relative.md)",
                        "MarkdownHelp.UnsafeLinks.Result"),
                    Entry(
                        "![alt](https://example.com/image.png)",
                        "MarkdownHelp.RemoteImage.Result"),
                    Entry(
                        "- [x] task\n[^1] footnote\n$math$",
                        "MarkdownHelp.UnsupportedExtensions.Result")
                ])
        ];

        private static MarkdownSyntaxHelpEntry Entry(
            string syntax,
            string resourceKey) =>
            new(syntax, LocalizationManager.GetString(resourceKey));

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            groups = CreateGroups();
            OnPropertyChanged(nameof(Groups));
        }
    }
}
