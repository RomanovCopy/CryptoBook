using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;

namespace CryptoBook.Help;

public interface IHelpContentSource
{
    Stream Open(string path);
}

public sealed class ResourceHelpContentSource : IHelpContentSource
{
    public Stream Open(string path)
    {
        if (!HelpContentService.IsResourcePath(path))
            throw new InvalidDataException("Invalid help resource path.");
        return Application.GetResourceStream(new Uri(
            "/CryptoBook;component/Help/" + path, UriKind.Relative))?.Stream
            ?? throw new FileNotFoundException("Help resource is unavailable.");
    }
}

public sealed record HelpTopic(string Id, string Section, string Title,
    string Markdown, string Text, string[] Keywords, string[] Images);
public sealed record HelpSearchResult(string Id, string Title, string Snippet, int Rank);
public sealed record HelpLocale(string Culture, IReadOnlyDictionary<string, string> Sections,
    IReadOnlyList<HelpTopic> Topics);

/// <summary>Only catalogued, embedded content is readable; user Markdown uses its own policy.</summary>
public sealed class HelpContentService
{
    private readonly IHelpContentSource source;
    private readonly Catalog catalog;
    private readonly Dictionary<string, HelpLocale> locales = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    public HelpContentService(IHelpContentSource source)
    {
        this.source = source;
        catalog = ReadJson<Catalog>("catalog.json");
        if (catalog.SchemaVersion != 1 || catalog.Cultures.Length == 0 ||
            catalog.Topics.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Topics.Length ||
            catalog.Sections.Distinct(StringComparer.Ordinal).Count() != catalog.Sections.Length ||
            catalog.Illustrations.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Illustrations.Length)
            throw new InvalidDataException("Invalid help catalog.");
        foreach (var culture in catalog.Cultures)
            if (!Regex.IsMatch(culture, @"\A[a-z]{2}-[A-Z]{2}\z"))
                throw new InvalidDataException("Invalid help culture.");
        foreach (var topic in catalog.Topics)
            if (!Regex.IsMatch(topic.Id, @"\A[a-z]+\.[a-z]+\z") ||
                !catalog.Sections.Contains(topic.Section, StringComparer.Ordinal) ||
                topic.Status is not ("planned" or "sample" or "ready"))
                throw new InvalidDataException("Invalid help topic.");
        foreach (var image in catalog.Illustrations)
            if (!Regex.IsMatch(image.Id, @"\AS[0-9]{2}-[a-z]\z") ||
                image.File != $"images/{image.Id}.png")
                throw new InvalidDataException("Invalid help illustration.");
        // All published topics have a substantive version in every supported culture.
        foreach (string culture in catalog.Cultures)
            locales.Add(culture, ReadLocale(culture));
    }

    public IReadOnlyList<string> Cultures => catalog.Cultures;
    public IReadOnlyList<string> SectionOrder => catalog.Sections;
    public bool IsKnownTopic(string id) => catalog.Topics.Any(t => t.Id == id);
    public bool IsAvailable(string id) => locales.Values.All(l => l.Topics.Any(t => t.Id == id));
    public HelpLocale GetLocale(string culture) => locales.TryGetValue(culture, out var locale)
        ? locale : throw new InvalidDataException("Missing help translation.");
    public HelpTopic? Find(string culture, string id) => GetLocale(culture).Topics.FirstOrDefault(t => t.Id == id);

    public string? ResolveTopicLink(string url) => url.StartsWith("help:", StringComparison.Ordinal) &&
        IsKnownTopic(url[5..]) ? url[5..] : null;

    public BitmapSource? LoadImage(string culture, HelpTopic topic, string path)
    {
        if (!catalog.Cultures.Contains(culture, StringComparer.Ordinal) ||
            !topic.Images.Contains(path, StringComparer.Ordinal) || !IsResourcePath(path))
            return null;
        try
        {
            using Stream stream = source.Open(culture + "/" + path);
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                if (bytes.Length + read > 25L * 1024 * 1024) return null;
                bytes.Write(buffer, 0, read);
            }
            bytes.Position = 0;
            var frame = BitmapDecoder.Create(bytes, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
            int decodeWidth = Math.Min(1600, frame.PixelWidth);
            bytes.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodeWidth;
            image.StreamSource = bytes;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or
            NotSupportedException or InvalidOperationException or FormatException)
        { return null; }
    }

    public IReadOnlyList<HelpSearchResult> Search(string culture, string query)
    {
        string[] words = NormalizeText(query)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        CompareInfo compare = CultureInfo.GetCultureInfo(culture).CompareInfo;
        bool ContainsAll(string value)
        {
            string normalized = NormalizeText(value);
            return words.All(w => compare.IndexOf(normalized, w, CompareOptions.IgnoreCase) >= 0);
        }
        return GetLocale(culture).Topics.Where(t => ContainsAll(t.Title + " " +
                string.Join(' ', t.Keywords) + " " + t.Text))
            .Select(t => new HelpSearchResult(t.Id, t.Title, Snippet(t.Text, words, compare),
                ContainsAll(t.Title) ? 0 : ContainsAll(t.Title + " " + string.Join(' ', t.Keywords)) ? 1 : 2))
            .OrderBy(r => r.Rank).ToArray();
    }

    public static bool IsResourcePath(string path) =>
        Regex.IsMatch(path, @"\A[A-Za-z0-9.-]+(?:/[A-Za-z0-9.-]+)*\z") &&
        path.Split('/').All(p => p is not ("." or ".."));

    private HelpLocale ReadLocale(string culture)
    {
        var data = ReadJson<LocaleData>(culture + "/locale.json");
        if (data.Culture != culture || data.Topics.Select(t => t.Id).Distinct().Count() != data.Topics.Length ||
            !catalog.Sections.All(s => data.Sections.TryGetValue(s, out string? name) && !string.IsNullOrWhiteSpace(name)))
            throw new InvalidDataException("Invalid help locale.");
        var topics = new List<HelpTopic>();
        foreach (var entry in catalog.Topics.Where(t => t.Status != "planned"))
        {
            var local = data.Topics.SingleOrDefault(t => t.Id == entry.Id);
            if (local is null || local.File != entry.Id + ".md" || local.Keywords.Length == 0)
                throw new InvalidDataException("Missing help translation.");
            using var reader = new StreamReader(source.Open(culture + "/" + local.File), Encoding.UTF8);
            string markdown = reader.ReadToEnd();
            var parsed = Markdown.Parse(markdown, Pipeline);
            var headings = parsed.OfType<HeadingBlock>().Where(h => h.Level == 1).ToArray();
            if (headings.Length != 1 || markdown.Length < 100)
                throw new InvalidDataException("Invalid help article.");
            foreach (var link in parsed.Descendants<LinkInline>().Where(l => !l.IsImage))
                if (link.Url?.StartsWith("help:", StringComparison.Ordinal) == true && ResolveTopicLink(link.Url) is null)
                    throw new InvalidDataException("Unknown help link.");
            string[] images = entry.IllustrationIds.Select(id => catalog.Illustrations.Single(i => i.Id == id).File).ToArray();
            if (parsed.Descendants<LinkInline>().Where(l => l.IsImage).Any(l => !images.Contains(l.Url, StringComparer.Ordinal)))
                throw new InvalidDataException("Unknown help image.");
            topics.Add(new(entry.Id, entry.Section, InlineText(headings[0].Inline), markdown,
                PlainText(parsed), local.Keywords, images));
        }
        return new(culture, data.Sections, topics);
    }

    private T ReadJson<T>(string path)
    {
        using var stream = source.Open(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidDataException("Empty help metadata.");
    }

    private static string PlainText(MarkdownDocument document) => string.Join(' ', document
        .Descendants<LeafBlock>().Select(b => InlineText(b.Inline) +
            (b is CodeBlock code ? code.Lines.ToString() : string.Empty)));

    private static string InlineText(ContainerInline? container)
    {
        if (container is null) return string.Empty;
        var result = new StringBuilder();
        foreach (var inline in container)
            result.Append(inline switch
            {
                LiteralInline literal => literal.Content.ToString(),
                CodeInline code => code.Content,
                LineBreakInline => " ",
                LinkInline link => InlineText(link) + (link.IsImage ? " " + link.Title : ""),
                ContainerInline nested => InlineText(nested),
                _ => string.Empty
            });
        return result.ToString();
    }

    private static string Snippet(string text, string[] words, CompareInfo compare)
    {
        int index = words.Select(w => compare.IndexOf(text, w, CompareOptions.IgnoreCase)).FirstOrDefault(i => i >= 0);
        int start = Math.Max(0, index - 45);
        return (start > 0 ? "…" : "") + text.Substring(start, Math.Min(160, text.Length - start)) +
            (text.Length - start > 160 ? "…" : "");
    }

    private static string NormalizeText(string value)
    {
        try { return value.Normalize(NormalizationForm.FormC); }
        catch (ArgumentException)
        {
            return string.Concat(value.EnumerateRunes().Select(rune => rune.ToString()))
                .Normalize(NormalizationForm.FormC);
        }
    }

    private sealed class Catalog
    {
        public int SchemaVersion { get; set; }
        public string[] Cultures { get; set; } = [];
        public string[] Sections { get; set; } = [];
        public TopicEntry[] Topics { get; set; } = [];
        public Illustration[] Illustrations { get; set; } = [];
    }
    private sealed class TopicEntry
    {
        public string Id { get; set; } = "";
        public string Section { get; set; } = "";
        public string Status { get; set; } = "";
        public string[] IllustrationIds { get; set; } = [];
    }
    private sealed class Illustration
    {
        public string Id { get; set; } = "";
        public string File { get; set; } = "";
    }
    private sealed class LocaleData
    {
        public string Culture { get; set; } = "";
        public Dictionary<string, string> Sections { get; set; } = [];
        public LocalTopic[] Topics { get; set; } = [];
    }
    private sealed class LocalTopic
    {
        public string Id { get; set; } = "";
        public string File { get; set; } = "";
        public string[] Keywords { get; set; } = [];
    }
}
