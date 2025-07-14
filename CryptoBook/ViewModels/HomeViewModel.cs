using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class HomeViewModel: ViewModelBase, IHomeViewModel
    {
        private readonly HomeModel homeModel;
        private readonly ILifetimeScope scope;

        public Action<object> BehaviorReady { get => behaviorReady; set => behaviorReady = value; }
        Action<object> behaviorReady;

        public HomeViewModel(ILifetimeScope scope)
        {
            this.scope = scope;
            homeModel = new(scope);
            homeModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        public ICommand PageLoaded => pageLoaded ??= new RelayCommand(homeModel.Execute_PageLoaded, homeModel.CanExecute_PageLoded);
        RelayCommand pageLoaded;

        public ICommand PageClear => pageClear ??= new RelayCommand(homeModel.Execute_PageClear, homeModel.CanExecute_PageClear);
        RelayCommand pageClear;

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}
