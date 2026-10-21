using CryptoBook.Infrastructure;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class DocumentSearchViewModel: ViewModelBase
    {
        private bool isOpen;
        private string query = string.Empty;
        private bool matchCase;
        private int resultCount;
        private int currentIndex = -1;
        private readonly RelayCommand next;
        private readonly RelayCommand previous;

        public DocumentSearchViewModel()
        {
            Open = new RelayCommand(_ =>
            {
                IsOpen = true;
                FocusRequested?.Invoke(this, EventArgs.Empty);
            });
            Close = new RelayCommand(_ => IsOpen = false);
            next = new RelayCommand(_ => Move(1), _ => ResultCount > 0);
            previous = new RelayCommand(_ => Move(-1), _ => ResultCount > 0);
        }

        public event EventHandler? FocusRequested;

        public bool IsOpen
        {
            get => isOpen;
            private set => SetProperty(ref isOpen, value);
        }

        public string Query
        {
            get => query;
            set => SetProperty(ref query, value ?? string.Empty);
        }

        public bool MatchCase
        {
            get => matchCase;
            set => SetProperty(ref matchCase, value);
        }

        public int ResultCount => resultCount;
        public int CurrentIndex => currentIndex;
        public string Status => string.IsNullOrEmpty(Query) ? string.Empty :
            ResultCount == 0 ? LocalizationManager.GetString("DocumentSearch.NotFound") :
            LocalizationManager.Format("DocumentSearch.Count", CurrentIndex + 1, ResultCount);

        public ICommand Open { get; }
        public ICommand Close { get; }
        public ICommand Next => next;
        public ICommand Previous => previous;

        internal void SetResultCount(int count)
        {
            resultCount = count;
            currentIndex = count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(ResultCount), nameof(CurrentIndex), nameof(Status));
            next.RaiseCanExecuteChanged();
            previous.RaiseCanExecuteChanged();
        }

        private void Move(int direction)
        {
            if(ResultCount == 0)
                return;
            currentIndex = (CurrentIndex + direction + ResultCount) % ResultCount;
            OnPropertyChanged(nameof(CurrentIndex), nameof(Status));
        }
    }
}
