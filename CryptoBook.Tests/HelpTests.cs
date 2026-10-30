using CryptoBook.Help;
using CryptoBook.Infrastructure;
using CryptoBook.Services;
using CryptoBook.Validation;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace CryptoBook.Tests;

public sealed class HelpTests
{
    [Theory]
    [InlineData("../secret.png")]
    [InlineData("images/../../secret.png")]
    [InlineData("images/%2e%2e/secret.png")]
    [InlineData("images%2fsecret.png")]
    [InlineData("C:/secret.png")]
    [InlineData("\\server\\share.png")]
    [InlineData("https://example.com/image.png")]
    [InlineData("pack://application:,,,/Other;component/image.png")]
    [InlineData("images/a.png?query")]
    [InlineData("images/a.png\n")]
    public void ResourcePath_RejectsAnythingOutsideThePackagedNamespace(string path)
        => Assert.False(HelpContentService.IsResourcePath(path));

    [Theory]
    [InlineData("catalog.json")]
    [InlineData("ru-RU/doc.create.md")]
    [InlineData("de-DE/images/S04-a.png")]
    public void ResourcePath_AcceptsCanonicalPackagePaths(string path)
        => Assert.True(HelpContentService.IsResourcePath(path));

    [Fact]
    public void PlannedTopics_AreRegisteredButNotLoadedOrSearched()
    {
        var source = new MemorySource();
        var content = new HelpContentService(source);
        Assert.True(content.IsKnownTopic("doc.save"));
        Assert.False(content.IsAvailable("doc.save"));
        Assert.Single(content.GetLocale("en-US").Topics);
        Assert.Empty(content.Search("en-US", "save"));
        Assert.Equal("doc.save", content.ResolveTopicLink("help:doc.save"));
        Assert.Null(content.ResolveTopicLink("help:missing.topic"));
        Assert.Null(content.ResolveTopicLink("help:%64oc.save"));
        Assert.DoesNotContain(source.Reads, p => p.EndsWith("doc.save.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Search_UsesVisibleTextAndKeywords_AndRequiresEveryWord()
    {
        var content = new HelpContentService(new MemorySource());
        Assert.Single(content.Search("en-US", "  WINDOW  "));
        Assert.Single(content.Search("en-US", "09:00 departure"));
        Assert.Empty(content.Search("en-US", "09:00 nonexistent"));
        Assert.Empty(content.Search("en-US", ""));
        Assert.Empty(content.Search("en-US", "private-address"));
    }

    [Fact]
    public void Search_NormalizesUnicode_AndToleratesIncompletePastedSurrogates()
    {
        var source = new MemorySource(); source.Files["en-US/start.interface.md"] += "\nCafé.";
        var content = new HelpContentService(source);
        Assert.Single(content.Search("en-US", "Cafe\u0301"));
        Assert.Empty(content.Search("en-US", "\ud83d"));
    }

    [Fact]
    public void MissingTranslation_FailsRatherThanSubstitutingAnotherLanguage()
    {
        var source = new MemorySource(); source.Files.Remove("en-US/start.interface.md");
        Assert.Throws<FileNotFoundException>(() => new HelpContentService(source));
    }

    [Fact]
    public void UnknownInternalLink_FailsCatalogValidation()
    {
        var source = new MemorySource(); source.Files["en-US/start.interface.md"] += "\n[missing](help:missing.topic)";
        Assert.Throws<InvalidDataException>(() => new HelpContentService(source));
    }

    [Fact]
    public void Metadata_CannotSelectAnArbitraryFile()
    {
        var source = new MemorySource(); source.Files["en-US/locale.json"] = source.Files["en-US/locale.json"].Replace("start.interface.md", "../private.md");
        Assert.Throws<InvalidDataException>(() => new HelpContentService(source));
    }

    [Theory]
    [InlineData(-10000, -10000, 0, 0)]
    [InlineData(10000, 10000, 1120, 780)]
    [InlineData(double.NaN, double.NaN, double.NaN, double.NaN)]
    public void Placement_RecoversInvalidOrDisconnectedMonitorBounds(double left, double top, double width, double height)
    {
        Rect work = new(0, 0, 900, 600);
        Rect placement = HelpWindowPlacement.Clamp(new(Left: left, Top: top, Width: width, Height: height), work);
        Assert.True(work.Contains(placement)); Assert.True(placement.Width >= 720); Assert.True(placement.Height >= 480);
    }

    [WpfFact]
    public void PackagedTranslations_History_Search_CultureAndRendererPolicies_WorkTogether()
    {
        MemorySettingsProvider.Install();
        var app = System.Windows.Application.Current ?? new System.Windows.Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        LocalizationManager.SelectCulture("en-US");
        var content = new HelpContentService(new ResourceHelpContentSource());
        Assert.Equal(4, content.Cultures.Count);
        Assert.Equal(LocalizationManager.AvailableLanguages.Select(l => l.CultureName).Order(), content.Cultures.Order());
        foreach (string culture in content.Cultures)
        {
            Assert.Equal(24, content.GetLocale(culture).Topics.Count);
            Assert.Single(content.Search(culture, "09:00"));
            var topic = content.Find(culture, "doc.create")!;
            Assert.Null(content.LoadImage(culture, topic, "../images/S04-a.png"));
            Assert.Null(content.LoadImage(culture, topic, "images/%53%30%34-a.png"));
            foreach (string path in topic.Images) Assert.NotNull(content.LoadImage(culture, topic, path));
        }
        var preferences = new MemoryPreferences();
        using var model = new HelpWindowViewModel(content, preferences);
        Assert.Equal("start.interface", model.CurrentTopicId);
        model.ScrollOffset = 140;
        Assert.True(model.OpenTopic("doc.create"));
        model.Back.Execute(null); Assert.Equal(140, model.ScrollOffset);
        model.Forward.Execute(null); Assert.Equal("doc.create", model.CurrentTopicId);
        Assert.False(model.OpenTopic("files.navigate"));
        Assert.True(content.IsAvailable("doc.save"));
        model.Query = "09:00"; model.RunSearch(); Assert.Equal("doc.create", Assert.Single(model.Results).Id);
        model.Query = "nothing-matches-this-query"; model.RunSearch(); Assert.True(model.HasNoResults);
        LocalizationManager.SelectCulture("de-DE");
        Assert.Empty(model.Query); Assert.Empty(model.Results);
        Assert.Equal("doc.create", model.CurrentTopicId); Assert.Equal(0, model.ScrollOffset);
        model.Back.Execute(null); Assert.Equal("start.interface", model.CurrentTopicId); Assert.Equal(0, model.ScrollOffset);
        model.OpenTopic("doc.create");
        Assert.False(model.Forward.CanExecute(null));
        Assert.Contains("Dokument", new TextRange(model.Article.ContentStart, model.Article.ContentEnd).Text);
        model.SaveWindowState(new Rect(10, 20, 900, 650), 260);
        Assert.Equal("doc.create", preferences.State.Topic);
        var renderer = new MarkdownFlowDocumentRenderer();
        FlowDocument userDocument = renderer.Render("[topic](help:doc.create)", null);
        Assert.DoesNotContain(userDocument.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines), i => i is Hyperlink);
        Assert.Contains("topic", new TextRange(userDocument.ContentStart, userDocument.ContentEnd).Text);
        LocalizationManager.SelectCulture("en-US");
    }

    private sealed class MemoryPreferences : IHelpPreferences
    {
        public HelpWindowState State { get; private set; } = new();
        public HelpWindowState Load() => State;
        public void Save(HelpWindowState state) => State = state;
    }
    private sealed class MemorySource : IHelpContentSource
    {
        public Dictionary<string, string> Files { get; } = new()
        {
            ["catalog.json"] = """{"schemaVersion":1,"cultures":["en-US"],"sections":["start","doc"],"topics":[{"id":"start.interface","section":"start","status":"sample"},{"id":"doc.save","section":"doc","status":"planned"}],"illustrations":[]}""",
            ["en-US/locale.json"] = """{"culture":"en-US","sections":{"start":"Getting started","doc":"Documents"},"topics":[{"id":"start.interface","file":"start.interface.md","keywords":["window"]}]}""",
            ["en-US/start.interface.md"] = "# Main areas\n\nDeparture at 09:00. Find your way around the ordinary editor and its menu. " +
                "Use a demonstration document to explore the available controls. [link](https://private-address.example)"
        };
        public List<string> Reads { get; } = [];
        public Stream Open(string path)
        {
            Reads.Add(path);
            return Files.TryGetValue(path, out string? value) ? new MemoryStream(Encoding.UTF8.GetBytes(value)) : throw new FileNotFoundException(path);
        }
    }
}
