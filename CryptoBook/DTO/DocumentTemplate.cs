using CryptoBook.Infrastructure;

namespace CryptoBook.DTO;

/// <summary>Содержимое нового документа, независимое от формата файла и размера листа.</summary>
public sealed class DocumentTemplate(string id): ViewModelBase
{
    public string Id { get; } = id;
    public const string EmptyId = "empty";
    public string DisplayName => LocalizationManager.GetString($"DocumentTemplate.{Id}.Name");
    public string Description => LocalizationManager.GetString($"DocumentTemplate.{Id}.Description");
    public string Markdown => Id == EmptyId
        ? string.Empty : LocalizationManager.GetString($"DocumentTemplate.{Id}.Content");

    internal void RefreshLocalization() =>
        OnPropertyChanged(nameof(DisplayName), nameof(Description), nameof(Markdown));
}
