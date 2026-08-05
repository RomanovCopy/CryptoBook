using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System.IO;

namespace CryptoBook.ViewModels
{
    public sealed class PinnedDocumentItemViewModel: ViewModelBase
    {
        private bool isCurrent;
        private bool isDirty;
        private bool isOpening;
        private bool isAvailable;
        private int sortOrder;

        public PinnedDocumentItemViewModel(PinnedDocument document)
        {
            Path = document.Path;
            PinnedAtUtc = document.PinnedAtUtc;
            LastOpenedAtUtc = document.LastOpenedAtUtc;
            sortOrder = document.SortOrder;
            FileName = System.IO.Path.GetFileName(Path);
            if(string.IsNullOrWhiteSpace(FileName))
                FileName = Path;

            ParentDirectory = System.IO.Path.GetDirectoryName(Path) ?? Path;
            isAvailable = File.Exists(Path);
            Glyph = GetGlyph(System.IO.Path.GetExtension(Path));
        }

        public string Path { get; }
        public string FileName { get; }
        public string ParentDirectory { get; }
        public string Glyph { get; }
        public DateTimeOffset PinnedAtUtc { get; }
        public DateTimeOffset? LastOpenedAtUtc { get; }
        public int SortOrder => sortOrder;
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
        public bool CanMoveUp => SortOrder > 0;
        public bool CanMoveDown { get; private set; }

        public bool IsCurrent
        {
            get => isCurrent;
            private set => SetProperty(ref isCurrent, value);
        }

        public bool IsDirty
        {
            get => isDirty;
            private set => SetProperty(ref isDirty, value);
        }

        public bool IsOpening
        {
            get => isOpening;
            set => SetProperty(ref isOpening, value);
        }

        public void UpdateDocumentState(string? currentPath, bool currentIsDirty)
        {
            IsCurrent = PathsEqual(Path, currentPath);
            IsDirty = IsCurrent && currentIsDirty;
        }

        public void RefreshAvailability() => IsAvailable = File.Exists(Path);

        public void UpdateOrdering(int index, int itemCount)
        {
            bool oldCanMoveUp = CanMoveUp;
            bool oldCanMoveDown = CanMoveDown;
            sortOrder = index;
            CanMoveDown = index < itemCount - 1;
            OnPropertyChanged(nameof(SortOrder));
            if(oldCanMoveUp != CanMoveUp)
                OnPropertyChanged(nameof(CanMoveUp));
            if(oldCanMoveDown != CanMoveDown)
                OnPropertyChanged(nameof(CanMoveDown));
        }

        private static bool PathsEqual(string left, string? right)
        {
            if(string.IsNullOrWhiteSpace(right))
                return false;

            try
            {
                return string.Equals(
                    left,
                    System.IO.Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch(Exception exception) when(
                exception is ArgumentException or NotSupportedException)
            {
                return false;
            }
        }

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
