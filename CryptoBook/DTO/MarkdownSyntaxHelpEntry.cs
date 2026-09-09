namespace CryptoBook.DTO
{
    public sealed record MarkdownSyntaxHelpEntry(
        string Syntax,
        string Result);

    public sealed record MarkdownSyntaxHelpGroup(
        string Title,
        IReadOnlyList<MarkdownSyntaxHelpEntry> Entries);
}
