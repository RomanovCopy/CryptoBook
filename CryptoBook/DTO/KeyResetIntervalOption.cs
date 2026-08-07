namespace CryptoBook.DTO;

public sealed record KeyResetIntervalOption(int Minutes, string DisplayName)
{
    public static IReadOnlyList<KeyResetIntervalOption> All { get; } =
    [
        new(1, "1 минута"),
        new(5, "5 минут"),
        new(10, "10 минут"),
        new(15, "15 минут"),
        new(30, "30 минут"),
        new(0, "Никогда")
    ];
}
