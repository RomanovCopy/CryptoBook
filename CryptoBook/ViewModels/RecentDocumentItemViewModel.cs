using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System.Globalization;
using System.IO;

namespace CryptoBook.ViewModels
{
    public sealed class RecentDocumentItemViewModel: ViewModelBase
    {
        private bool isAvailable;
        private bool isOpening;

        public RecentDocumentItemViewModel(RecentDocument document)
        {
            Path = document.Path;
            LastAccessedAtUtc = document.LastAccessedAtUtc;
            OpenCount = document.OpenCount;
            FileName = System.IO.Path.GetFileName(Path);
            if(string.IsNullOrWhiteSpace(FileName))
                FileName = Path;

            ParentDirectory = System.IO.Path.GetDirectoryName(Path) ?? Path;
            Glyph = GetGlyph(System.IO.Path.GetExtension(Path));
            isAvailable = File.Exists(Path);
        }

        public string Path { get; }
        public string FileName { get; }
        public string ParentDirectory { get; }
        public string Glyph { get; }
        public DateTimeOffset LastAccessedAtUtc { get; }
        public int OpenCount { get; }
        public string LastAccessedDisplay => LastAccessedAtUtc
            .ToLocalTime()
            .ToString("g", CultureInfo.CurrentCulture);

        public bool IsAvailable
        {
            get => isAvailable;
            private set
            {
                if(!SetProperty(ref isAvailable, value))
                    return;

                OnPropertyChanged(nameof(IsMissing));
            }
        }

        public bool IsMissing => !IsAvailable;

        public bool IsOpening
        {
            get => isOpening;
            set => SetProperty(ref isOpening, value);
        }

        public void RefreshAvailability() => IsAvailable = File.Exists(Path);

        private static string GetGlyph(string extension) =>
            extension.ToLowerInvariant() switch
            {
                ".cbook" or ".cbox" => "\uE72E",
                ".pdf" => "\uEA90",
                ".rtf" or ".txt" or ".xaml" or ".xamlpackage" => "\uE8A5",
                _ => "\uE7C3"
            };
    }
}
