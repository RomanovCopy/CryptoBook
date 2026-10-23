using CryptoBook.AI;
using CryptoBook.Behaviors;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Xunit;

namespace CryptoBook.Tests;

public sealed class AiChatMarkdownTests
{
    [WpfFact]
    public void Markdown_RendersStructureAndChatTypography_WithoutDocumentPageWidth()
    {
        const string source = "# Heading\n\n**bold** and *italic* with `inline`\n\n- item\n\n> quote\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```cs\nint n = 1;\n```";
        var foreground = Brushes.DarkGreen;
        var document = AiChatDocumentRenderer.Render(source, true, new FontFamily("Consolas"), 20, foreground, "Courier New", "#123456");
        Assert.Contains(document.Blocks, block => block is System.Windows.Documents.List);
        Assert.Contains(document.Blocks, block => block is Table);
        Assert.Contains(document.Blocks, block => block is Section);
        Assert.Equal(40, Assert.IsType<Paragraph>(document.Blocks.FirstBlock).FontSize);
        Assert.Equal("Consolas", document.FontFamily.Source);
        Assert.Equal(20, document.FontSize);
        Assert.Same(foreground, document.Foreground);
        Assert.Equal(Brushes.Transparent, document.Background);
        Assert.Equal(new Thickness(0), document.PagePadding);
        Assert.Equal(0, document.MinPageWidth);
        Assert.True(double.IsPositiveInfinity(document.MaxPageWidth));
        Assert.True(double.IsNaN(document.PageWidth));
        string displayed = new TextRange(document.ContentStart, document.ContentEnd).Text;
        Assert.Contains("int n = 1;", displayed);
        Assert.DoesNotContain("**bold**", displayed);
        var code = document.Blocks.OfType<Paragraph>().Single(paragraph => paragraph.Inlines.OfType<Run>().Any(run => run.Text.Contains("int n")));
        Assert.Equal("Courier New", code.FontFamily.Source);
        Assert.Equal("#FF123456", code.Foreground.ToString());
        var inline = Descendants(document).OfType<Run>().Single(run => run.Text == "inline");
        Assert.Equal(AiChatAppearance.DefaultCodeFontFamily, inline.FontFamily.Source);
        Assert.Same(foreground, inline.Foreground);
        var preview = new CryptoBook.Services.MarkdownFlowDocumentRenderer().Render("```\ncode\n```", null);
        Assert.Equal(AiChatAppearance.DefaultCodeFontFamily, Assert.IsType<Paragraph>(preview.Blocks.FirstBlock).FontFamily.Source);
    }

    [WpfFact]
    public void MarkdownDisabled_PreservesSourceSyntaxAndLineBreaks_AndDoesNotMutateSource()
    {
        const string source = "# Heading\n\n**bold**\nline two\n```cs\nvalue\n```";
        var document = AiChatDocumentRenderer.Render(source, false, new FontFamily("Segoe UI"), 16, Brushes.Black);
        Assert.IsType<Paragraph>(Assert.Single(document.Blocks.Cast<Block>()));
        string displayed = new TextRange(document.ContentStart, document.ContentEnd).Text.Replace("\r\n", "\n").TrimEnd('\n');
        Assert.Equal(source, displayed);
    }

    [WpfFact]
    public void Markdown_DoesNotLoadImagesOrActivateHtml_AndRejectsLocalAndScriptLinks()
    {
        const string source = "![remote](https://example.com/private.png) ![local](file:///C:/private.png)\n\n[script](javascript:alert(1)) [file](file:///C:/private.txt) [web](https://example.com/)\n\n<script>alert(1)</script>";
        var document = AiChatDocumentRenderer.Render(source, true, new FontFamily("Segoe UI"), 14, Brushes.Black);
        var elements = Descendants(document).ToArray();
        Assert.DoesNotContain(elements, element => element is InlineUIContainer);
        var link = Assert.Single(elements.OfType<Hyperlink>());
        Assert.Equal("https", link.NavigateUri.Scheme);
        Assert.Contains("<script>", new TextRange(document.ContentStart, document.ContentEnd).Text);
    }

    [WpfFact]
    public async Task MarkdownBehavior_UpdatesStreamAndFormat_AndResumesWithLatestTextAfterUnload()
    {
        var viewer = new RichTextBox { IsReadOnly = true, IsUndoEnabled = false, FontSize = 14 };
        AiChatMarkdownBehavior.SetText(viewer, "# First");
        var window = new Window { Content = viewer, Width = 400, Height = 400, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            window.Show();
            await Task.Delay(300);
            Assert.Equal(28, Assert.IsType<Paragraph>(viewer.Document.Blocks.FirstBlock).FontSize);
            AiChatMarkdownBehavior.SetText(viewer, "# First\n\n```cs\npartial");
            await Task.Delay(300);
            Assert.Contains("partial", Read(viewer));
            const string complete = "# First\n\n```cs\ncomplete\n```";
            AiChatMarkdownBehavior.SetText(viewer, complete);
            AiChatMarkdownBehavior.SetRenderMarkdown(viewer, false);
            viewer.FontFamily = new FontFamily("Consolas"); viewer.FontSize = 20; viewer.Foreground = Brushes.DarkBlue;
            await WaitUntil(() => Read(viewer).Contains("```") && viewer.Document.FontSize == 20 &&
                viewer.Document.Foreground == Brushes.DarkBlue);
            Assert.Contains("```", Read(viewer));
            Assert.Equal(20, viewer.Document.FontSize);
            Assert.Equal("Consolas", viewer.Document.FontFamily.Source);
            Assert.Same(Brushes.DarkBlue, viewer.Document.Foreground);
            Assert.Equal(complete, AiChatMarkdownBehavior.GetText(viewer));
            window.Content = null;
            await Task.Delay(50);
            var previous = viewer.Document;
            AiChatMarkdownBehavior.SetText(viewer, "## After reload");
            AiChatMarkdownBehavior.SetRenderMarkdown(viewer, true);
            await Task.Delay(300);
            Assert.Same(previous, viewer.Document);
            window.Content = viewer;
            await Task.Delay(300);
            Assert.DoesNotContain("##", Read(viewer));
            Assert.Contains("After reload", Read(viewer));
            AiChatMarkdownBehavior.SetText(viewer, "```cs\nstyled\n```");
            AiChatMarkdownBehavior.SetCodeFontFamily(viewer, "Courier New");
            AiChatMarkdownBehavior.SetCodeColor(viewer, "#123456");
            await WaitUntil(() => viewer.Document.Blocks.OfType<Paragraph>().Any(paragraph =>
                paragraph.FontFamily.Source == "Courier New" && paragraph.Foreground.ToString() == "#FF123456"));
            AiChatMarkdownBehavior.SetCodeColor(viewer, "");
            await WaitUntil(() => viewer.Document.Blocks.OfType<Paragraph>().Any(paragraph =>
                paragraph.FontFamily.Source == "Courier New" && paragraph.Foreground == Brushes.DarkBlue));
        }
        finally { window.Close(); }
    }

    [WpfFact]
    public void MarkdownSetting_DefaultsToEnabled_AndPersistsBothChoicesAndReset()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CryptoBook-AI-Markdown-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new AiSettingsStore(path);
            var settings = new AiChatAppearanceViewModel(store);
            Assert.True(settings.RenderMarkdown);
            settings.RenderMarkdown = false; settings.Save.Execute(null);
            Assert.False(new AiSettingsStore(path).Current.ChatAppearance.RenderMarkdown);
            settings.RenderMarkdown = true; settings.Save.Execute(null);
            Assert.True(new AiSettingsStore(path).Current.ChatAppearance.RenderMarkdown);
            settings.RenderMarkdown = false;
            settings.CodeFontFamily = "Courier New"; settings.CodeColor = "#123456";
            settings.Reset.Execute(null); settings.Save.Execute(null);
            Assert.True(new AiSettingsStore(path).Current.ChatAppearance.RenderMarkdown);
            Assert.Equal(AiChatAppearance.DefaultCodeFontFamily, new AiSettingsStore(path).Current.ChatAppearance.CodeFontFamily);
            Assert.Empty(new AiSettingsStore(path).Current.ChatAppearance.CodeColor);
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static string Read(RichTextBox viewer) => new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;
    private static async Task WaitUntil(Func<bool> ready)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while(!ready() && DateTime.UtcNow < deadline) await Task.Delay(50);
        Assert.True(ready(), "The rendered message did not reach the expected state.");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach(object child in LogicalTreeHelper.GetChildren(parent))
        {
            if(child is not DependencyObject element) continue;
            yield return element;
            foreach(var descendant in Descendants(element)) yield return descendant;
        }
    }
}
