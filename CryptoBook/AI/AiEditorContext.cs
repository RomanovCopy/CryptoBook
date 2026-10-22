using CryptoBook.Interfaces;
using CryptoBook.Security;
using CryptoBook.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TextBox = System.Windows.Controls.TextBox;
using DataFormats = System.Windows.DataFormats;

namespace CryptoBook.AI;

public sealed record AiEditTarget(string SelectionText, string DocumentText, string Name,
    Func<string, bool, bool> Apply)
{
    public bool HasSelection => !string.IsNullOrEmpty(SelectionText);
}

public sealed class AiEditorContext : IDisposable
{
    private readonly IRichTextBoxService richText;
    private readonly IMarkdownDocumentState markdown;
    private readonly IDocumentSession session;
    private IPageNavigationService navigation;
    private TextBox? sourceEditor;
    private bool disposed;
    public event EventHandler? Changed;

    public AiEditorContext(IRichTextBoxService richText, IMarkdownDocumentState markdown,
        IDocumentSession session, IPageNavigationService navigation)
    {
        this.richText = richText; this.markdown = markdown; this.session = session; this.navigation = navigation;
        richText.Service.SelectionChanged += OnSelectionChanged;
        richText.Service.IsVisibleChanged += OnVisibilityChanged;
        session.PropertyChanged += OnStateChanged;
        markdown.PropertyChanged += OnStateChanged;
        navigation.PropertyChanged += OnStateChanged;
        ObserveSourceEditor();
    }

    public bool CanInsert => navigation.CurrentKey == "Home" && !markdown.IsActive && !richText.IsReadOnly &&
        (session.HasDocument || richText.Service.IsVisible) ||
        navigation.CurrentKey == "MarkdownEditor" && markdown.IsActive &&
        FindSourceEditor() is { IsVisible: true, IsReadOnly: false };
    public bool CanReplace => CanInsert && (markdown.IsActive
        ? FindSourceEditor()?.SelectionLength > 0 : !string.IsNullOrEmpty(richText.Selection.Text));

    public void BindWorkspace(IPageNavigationService activeNavigation)
    {
        navigation.PropertyChanged -= OnStateChanged;
        navigation = activeNavigation;
        navigation.PropertyChanged += OnStateChanged;
        ObserveSourceEditor();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public AiEditTarget? Capture()
    {
        if(!session.HasDocument && !(navigation.CurrentKey == "Home" && richText.Service.IsVisible)) return null;
        long revision = session.Revision;
        string? pageKey = navigation.CurrentKey;
        string name = session.DisplayName;
        if(pageKey == "MarkdownEditor" && markdown.IsActive)
        {
            string original = markdown.Text;
            long documentVersion = markdown.DocumentVersion;
            var editor = FindSourceEditor();
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
                    editor.Select(editor.SelectionStart + editor.SelectionLength, 0);
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
                // Render an independent fragment once. Later edits belong to
                // the rich-text document and are never rebuilt from AI source.
                var fragment = new MarkdownFlowDocumentRenderer().Render(answer, null);
                AiDocumentFragmentFormatter.Apply(fragment, document);
                using var stream = new SensitiveMemoryStream();
                var lastBlock = fragment.Blocks.LastBlock!;
                var end = lastBlock is Paragraph ? lastBlock.ContentEnd : lastBlock.ElementEnd;
                new TextRange(fragment.ContentStart, end).Save(stream, DataFormats.XamlPackage, true);
                stream.Position = 0;
                richText.BeginChange();
                try
                {
                    var target = replace ? range : new TextRange(range.End, range.End);
                    target.Load(stream, DataFormats.XamlPackage);
                    richText.Selection.Select(target.End, target.End);
                }
                finally { richText.EndChange(); }
                richText.Focus();
                return true;
            });
    }

    private TextBox? FindSourceEditor() => navigation.CurrentPage?.FindName("SourceEditor") as TextBox;
    private void ObserveSourceEditor()
    {
        var current = navigation.CurrentKey == "MarkdownEditor" ? FindSourceEditor() : null;
        if(ReferenceEquals(sourceEditor, current)) return;
        if(sourceEditor is not null)
        {
            sourceEditor.SelectionChanged -= OnSelectionChanged;
            sourceEditor.IsVisibleChanged -= OnVisibilityChanged;
        }
        sourceEditor = current;
        if(sourceEditor is not null)
        {
            sourceEditor.SelectionChanged += OnSelectionChanged;
            sourceEditor.IsVisibleChanged += OnVisibilityChanged;
        }
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        ObserveSourceEditor();
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private void OnSelectionChanged(object sender, RoutedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    public void Dispose()
    {
        if(disposed) return;
        disposed = true;
        richText.Service.SelectionChanged -= OnSelectionChanged;
        richText.Service.IsVisibleChanged -= OnVisibilityChanged;
        session.PropertyChanged -= OnStateChanged;
        markdown.PropertyChanged -= OnStateChanged;
        navigation.PropertyChanged -= OnStateChanged;
        if(sourceEditor is not null)
        {
            sourceEditor.SelectionChanged -= OnSelectionChanged;
            sourceEditor.IsVisibleChanged -= OnVisibilityChanged;
        }
    }
}
