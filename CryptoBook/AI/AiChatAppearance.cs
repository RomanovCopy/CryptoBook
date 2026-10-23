using System.Text.Json.Serialization;

namespace CryptoBook.AI;

public sealed record AiChatAppearance
{
    internal const string DefaultCodeFontFamily = "Cascadia Mono, Consolas";
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 14;
    public string CodeFontFamily { get; init; } = DefaultCodeFontFamily;
    public string CodeColor { get; init; } = "";
    // Empty colors follow the active theme.
    public string UserColor { get; init; } = "";
    public string AssistantColor { get; init; } = "";
    public bool ShowMessageBorders { get; init; }
    public bool RenderMarkdown { get; init; } = true;
    [JsonIgnore] public bool HasUserColor => UserColor.Length > 0;
    [JsonIgnore] public bool HasAssistantColor => AssistantColor.Length > 0;

    internal AiChatAppearance Normalize() => this with
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 200 ? "Segoe UI" : FontFamily,
        FontSize = double.IsFinite(FontSize) && FontSize is >= 8 and <= 40 ? FontSize : 14,
        CodeFontFamily = string.IsNullOrWhiteSpace(CodeFontFamily) || CodeFontFamily.Length > 200 ? DefaultCodeFontFamily : CodeFontFamily,
        CodeColor = ValidColor(CodeColor) ? CodeColor.ToUpperInvariant() : "",
        UserColor = ValidColor(UserColor) ? UserColor.ToUpperInvariant() : "",
        AssistantColor = ValidColor(AssistantColor) ? AssistantColor.ToUpperInvariant() : ""
    };

    internal static bool ValidColor(string? value) => value is { Length: 7 } && value[0] == '#' &&
        value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
}
