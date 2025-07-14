using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class TitleBarRB_ViewModel:ViewModelBase, ITitleBarRB_ViewModel
    {
        private readonly ILifetimeScope scope;
        private readonly TitleBarRB_Model titleBarRB_Model;

        public TitleBarRB_ViewModel(ILifetimeScope scope)
        {
            this.scope = scope;
            titleBarRB_Model = new TitleBarRB_Model(scope);
            titleBarRB_Model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ObservableCollection<double> FontSizes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ICommand BoldCommand => throw new NotImplementedException();

        public ICommand ItalicCommand => throw new NotImplementedException();

        public ICommand UnderlineCommand => throw new NotImplementedException();

        public ICommand ClearFormattingCommand => throw new NotImplementedException();

        public ICommand InsertImageCommand => throw new NotImplementedException();

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();

        public ICommand Closed => throw new NotImplementedException();
    }
}
