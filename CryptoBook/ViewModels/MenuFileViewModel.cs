using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

namespace CryptoBook.ViewModels
{
    public class MenuFileViewModel:ViewModelBase, IMenuFileViewModel
    {
        private readonly MenuFileModel menuFileModel;
        public MenuFileViewModel()
        {
            menuFileModel = new MenuFileModel();
            menuFileModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand NewFile => throw new NotImplementedException();

        public ICommand OpenFile => throw new NotImplementedException();

        public ICommand SaveFile => throw new NotImplementedException();

        public ICommand SaveAsFile => throw new NotImplementedException();

        public ICommand FileOverview => throw new NotImplementedException();

        public ICommand OpenDirectory => throw new NotImplementedException();

        public ICommand WorkingDirectorySynchronization => throw new NotImplementedException();

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();
    }
}
