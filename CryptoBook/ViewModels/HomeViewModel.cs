using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class HomeViewModel: ViewModelBase, IHomeViewModel
    {
        private readonly HomeModel homeModel;
        private readonly ILifetimeScope scope;
        public IRichtextboxViewModel DocumentView { get; }

        public Action<object> BehaviorReady { get => behaviorReady; set => behaviorReady = value; }
        Action<object> behaviorReady;

        public HomeViewModel(
            ILifetimeScope scope,
            IRichtextboxViewModel documentView)
        {
            this.scope = scope;
            DocumentView = documentView ?? throw new ArgumentNullException(nameof(documentView));
            homeModel = new(scope);
            homeModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        public ICommand PageLoaded => pageLoaded ??= new RelayCommand(homeModel.Execute_PageLoaded, homeModel.CanExecute_PageLoded);
        RelayCommand pageLoaded;

        public ICommand PageClear => pageClear ??= new RelayCommand(homeModel.Execute_PageClear, homeModel.CanExecute_PageClear);
        RelayCommand pageClear;

        public ICommand Loaded => loaded ??=
            new RelayCommand(homeModel.Execute_PageLoaded, homeModel.CanExecute_PageLoded);
        RelayCommand loaded;

        public ICommand Close => close ??=
            new RelayCommand(homeModel.Execute_PageClose, homeModel.CanExecute_PageClose);
        RelayCommand close;

        public ICommand Closing => closing ??=
            new RelayCommand(_ => { });
        RelayCommand closing;

        public ICommand Closed => closed ??=
            new RelayCommand(_ => { });
        RelayCommand closed;
    }
}
