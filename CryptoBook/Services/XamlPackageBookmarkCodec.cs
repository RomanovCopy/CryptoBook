using CryptoBook.Security;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Documents;

namespace CryptoBook.Services;

/// <summary>
/// TextRange drops Span.Name/Tag and can merge inline boundaries. Persist bookmark
/// ranges in UTF-16 text coordinates, independently of WPF's structural symbols.
/// The text digest prevents applying stale metadata to a different document.
/// </summary>
internal static class XamlPackageBookmarkCodec
{
    internal const string EntryPath = "CryptoBook/Bookmarks.json";
    private const int MaximumMetadataLength = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Preserve(FlowDocument document, byte[] content)
    {
        var metadata = Capture(document);
        if(metadata.Bookmarks.Length == 0)
            return content;

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(metadata, jsonOptions);
        try
        {
            if(json.Length > MaximumMetadataLength)
                throw new InvalidDataException("Bookmark metadata is too large.");
            using var package = new SensitiveMemoryStream();
            package.Write(content);
            package.Position = 0;
            using(var archive = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true))
            {
                archive.GetEntry(EntryPath)?.Delete();
                using var output = archive.CreateEntry(EntryPath, CompressionLevel.Optimal).Open();
                output.Write(json);
            }
            return package.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    public static void Restore(FlowDocument document, ReadOnlyMemory<byte> content)
    {
        if(content.Length == 0)
            return;
        try
        {
            using var package = new SensitiveMemoryStream();
            package.Write(content.Span);
            package.Position = 0;
            using var archive = new ZipArchive(package, ZipArchiveMode.Read);
            var entry = archive.GetEntry(EntryPath);
            if(entry is null || entry.Length <= 0 || entry.Length > MaximumMetadataLength)
                return;
            using var input = entry.Open();
            var metadata = JsonSerializer.Deserialize<BookmarkMetadata>(input, jsonOptions);
            if(metadata is not null)
                Restore(document, metadata);
        }
        catch(Exception exception) when(exception is InvalidDataException or IOException or JsonException)
        {
            // Optional metadata must not prevent reading the text of older or damaged files.
        }
    }

    public static void Copy(FlowDocument source, FlowDocument destination) =>
        Restore(destination, Capture(source));

    private static BookmarkMetadata Capture(FlowDocument document)
    {
        var text = new StringBuilder();
        var anchors = new List<BookmarkRange>();
        var starts = new Dictionary<Span, int>();
        for(var p = document.ContentStart; p is not null && p.CompareTo(document.ContentEnd) < 0;)
        {
            var context = p.GetPointerContext(LogicalDirection.Forward);
            if(context == TextPointerContext.Text)
            {
                string run = p.GetTextInRun(LogicalDirection.Forward);
                text.Append(run);
                p = p.GetPositionAtOffset(run.Length);
                continue;
            }
            if(p.GetAdjacentElement(LogicalDirection.Forward) is Span span &&
               !string.IsNullOrEmpty(span.Name))
            {
                if(context == TextPointerContext.ElementStart)
                    starts[span] = text.Length;
                else if(context == TextPointerContext.ElementEnd && starts.Remove(span, out int start))
                    anchors.Add(new BookmarkRange(span.Name, start, text.Length - start, span.Tag as string));
            }
            p = p.GetNextContextPosition(LogicalDirection.Forward);
        }
        return new BookmarkMetadata(1, anchors.Count == 0 ? string.Empty : Digest(text.ToString()), anchors.ToArray());
    }

    private static void Restore(FlowDocument document, BookmarkMetadata metadata)
    {
        if(metadata.Version != 1 || metadata.Bookmarks is null)
            return;
        var text = new StringBuilder();
        var runs = new List<(int Offset, int Length, TextPointer Position)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for(var p = document.ContentStart; p is not null && p.CompareTo(document.ContentEnd) < 0;)
        {
            if(p.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                string run = p.GetTextInRun(LogicalDirection.Forward);
                runs.Add((text.Length, run.Length, p));
                text.Append(run);
                p = p.GetPositionAtOffset(run.Length);
                continue;
            }
            if(p.GetAdjacentElement(LogicalDirection.Forward) is Span span && !string.IsNullOrEmpty(span.Name))
                names.Add(span.Name);
            p = p.GetNextContextPosition(LogicalDirection.Forward);
        }
        if(!string.Equals(metadata.TextDigest, Digest(text.ToString()), StringComparison.Ordinal))
            return;

        // Resolve all pointers before wrapping ranges: inline structure may change,
        // while these live TextPointers continue to track their original characters.
        var ranges = new List<(BookmarkRange Bookmark, TextPointer Start, TextPointer End)>();
        var ends = new Stack<int>();
        foreach(var bookmark in metadata.Bookmarks.OrderBy(b => b?.Start).ThenByDescending(b => b?.Length))
        {
            if(bookmark is null || string.IsNullOrEmpty(bookmark.AnchorId) ||
               names.Contains(bookmark.AnchorId) || bookmark.Start < 0 || bookmark.Length <= 0 ||
               bookmark.Start > text.Length - bookmark.Length)
                continue;
            while(ends.TryPeek(out int boundary) && bookmark.Start >= boundary)
                ends.Pop();
            if(ends.TryPeek(out int parentEnd) && bookmark.Start + bookmark.Length > parentEnd)
                continue; // Crossing ranges cannot have originated from a valid Span tree.
            var startRun = FindRun(runs, bookmark.Start);
            int last = bookmark.Start + bookmark.Length - 1;
            var endRun = FindRun(runs, last);
            if(startRun < 0 || endRun < 0)
                continue;
            var start = runs[startRun].Position.GetPositionAtOffset(bookmark.Start - runs[startRun].Offset)!;
            var end = runs[endRun].Position.GetPositionAtOffset(last - runs[endRun].Offset + 1)!;
            if(start.Paragraph is null || start.Paragraph != end.Paragraph)
                continue;
            // Validate WPF names before changing the document.
            try { _ = new Span { Name = bookmark.AnchorId }; }
            catch(ArgumentException) { continue; }
            names.Add(bookmark.AnchorId);
            ends.Push(bookmark.Start + bookmark.Length);
            ranges.Add((bookmark, start, end));
        }
        foreach(var (bookmark, start, end) in ranges)
        {
            _ = new Span(start, end) { Name = bookmark.AnchorId, Tag = bookmark.Metadata };
        }
    }

    private static int FindRun(List<(int Offset, int Length, TextPointer Position)> runs, int offset)
    {
        int low = 0, high = runs.Count - 1;
        while(low <= high)
        {
            int middle = low + (high - low) / 2;
            var run = runs[middle];
            if(offset < run.Offset) high = middle - 1;
            else if(offset >= run.Offset + run.Length) low = middle + 1;
            else return middle;
        }
        return -1;
    }

    private static string Digest(string text)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(text);
        try { return Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private sealed record BookmarkMetadata(int Version, string TextDigest, BookmarkRange[] Bookmarks);
    private sealed record BookmarkRange(string AnchorId, int Start, int Length, string? Metadata);
}
