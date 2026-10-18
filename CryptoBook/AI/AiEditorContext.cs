using CryptoBook.Interfaces;
using System.Windows.Controls;
using System.Windows.Documents;
using TextBox = System.Windows.Controls.TextBox;

namespace CryptoBook.AI;

public sealed record AiEditTarget(string SelectionText, string DocumentText, string Name,
    Func<string, bool, bool> Apply)
{
    public bool HasSelection => !string.IsNullOrEmpty(SelectionText);
}

public sealed class AiEditorContext(IRichTextBoxService richText, IMarkdownDocumentState markdown,
    IDocumentSession session, IPageNavigationService navigation)
{
    public void BindWorkspace(IPageNavigationService activeNavigation) => navigation = activeNavigation;
    public AiEditTarget? Capture()
    {
        if(!session.HasDocument) return null;
        long revision = session.Revision;
        string? pageKey = navigation.CurrentKey;
        string name = session.DisplayName;
        if(pageKey == "MarkdownEditor" && markdown.IsActive)
        {
            string original = markdown.Text;
            long documentVersion = markdown.DocumentVersion;
            var editor = navigation.CurrentPage?.FindName("SourceEditor") as TextBox;
            int start = editor?.SelectionStart ?? 0;
            int length = editor?.SelectionLength ?? 0;
            string selected = editor?.SelectedText ?? string.Empty;
            bool wasEditable = editor?.IsVisible == true && !editor.IsReadOnly;
            return new(selected, original, name, (answer, replace) =>
            {
                if(!wasEditable || editor is null || !editor.IsVisible || editor.IsReadOnly ||
                   navigation.CurrentKey != pageKey ||
                   markdown.DocumentVersion != documentVersion || markdown.Text != original ||
                   session.Revision != revision || (replace && length == 0)) return false;
                editor.BeginChange();
                try
                {
                    editor.Select(replace ? start : start + length, replace ? length : 0);
                    editor.SelectedText = answer;
                }
                finally { editor.EndChange(); }
                editor.Focus();
                return true;
            });
        }
        if(pageKey != "Home" || markdown.IsActive) return null;
        FlowDocument document = richText.Document;
        var range = new TextRange(richText.Selection.Start, richText.Selection.End);
        string text = range.Text;
        return new(text, new TextRange(document.ContentStart, document.ContentEnd).Text, name,
            (answer, replace) =>
            {
                if(richText.IsReadOnly || navigation.CurrentKey != pageKey ||
                   !ReferenceEquals(richText.Document, document) || session.Revision != revision ||
                   range.Text != text || (replace && string.IsNullOrEmpty(text))) return false;
                richText.BeginChange();
                try
                {
                    var target = replace ? range : new TextRange(range.End, range.End);
                    target.Text = answer;
                    richText.Selection.Select(target.End, target.End);
                }
                finally { richText.EndChange(); }
                richText.Focus();
                return true;
            });
    }
}
