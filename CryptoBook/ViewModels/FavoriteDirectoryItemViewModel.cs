using CryptoBook.DTO;
using CryptoBook.Infrastructure;

namespace CryptoBook.ViewModels
{
    public sealed class FavoriteDirectoryItemViewModel: ViewModelBase
    {
        private bool _isAvailable;

        public FavoriteDirectoryItemViewModel(
            FavoriteDirectory favorite,
            string displayPath)
        {
            Id = favorite.Id;
            Path = favorite.Path;
            DisplayName = favorite.DisplayName;
            DisplayPath = displayPath;
        }

        public Guid Id { get; }
        public string Path { get; }
        public string DisplayName { get; }
        public string DisplayPath { get; }

        public bool IsAvailable
        {
            get => _isAvailable;
            set => SetProperty(ref _isAvailable, value);
        }

        public string AvailabilityText => IsAvailable
            ? DisplayPath
            : LocalizationManager.Format(
                "Preview.DirectoryUnavailable",
                DisplayPath,
                Environment.NewLine);

        public override void OnPropertyChanged(string prop = "")
        {
            base.OnPropertyChanged(prop);
            if(prop == nameof(IsAvailable))
                base.OnPropertyChanged(nameof(AvailabilityText));
        }
    }
}
