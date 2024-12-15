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
    public class MenuSettingsViewModel:ViewModelBase, IMenuSettingsViewModel
    {
        private readonly MenuSettingsModel menuSettingsModel;

        public MenuSettingsViewModel()
        {
            menuSettingsModel = new MenuSettingsModel();
            menuSettingsModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand SetFontWeight => throw new NotImplementedException();

        public ICommand SetFontFamily => throw new NotImplementedException();

        public ICommand SetFontSize => throw new NotImplementedException();

        public ICommand SetFontColor => throw new NotImplementedException();

        public ICommand SetFontBackColor => throw new NotImplementedException();

        public ICommand SetPaperColor => throw new NotImplementedException();

        public ICommand SetEncoding => throw new NotImplementedException();

        public ICommand Localization => throw new NotImplementedException();

        public ICommand Loaded => throw new NotImplementedException();

        public ICommand Close => throw new NotImplementedException();

        public ICommand Closing => throw new NotImplementedException();
    }
}
