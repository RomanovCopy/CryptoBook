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

        public ICommand SetFontWeight => setFontWeight ??= new RelayCommand(menuSettingsModel.Execute_SetFontWeight, menuSettingsModel.CanExecute_SetFontWeight);
        RelayCommand setFontWeight;

        public ICommand SetFontFamily => setFontFamily ??= new RelayCommand(menuSettingsModel.Execute_SetFontFamily, menuSettingsModel.CanExecute_SetFontFamily);
        RelayCommand setFontFamily;

        public ICommand SetFontSize => setFontSize ??= new RelayCommand(menuSettingsModel.Execute_SetFontSize, menuSettingsModel.CanExecute_SetFontSize);
        RelayCommand setFontSize;

        public ICommand SetFontColor => setFontColor ??= new RelayCommand(menuSettingsModel.Execute_SetFontColor, menuSettingsModel.CanExecute_SetFontColor);
        RelayCommand setFontColor;


        public ICommand SetFontBackColor => setFontBackColor ??= new RelayCommand(menuSettingsModel.Execute_SetFontBackColor, menuSettingsModel.CanExecute_SetFontBackColor);
        RelayCommand setFontBackColor;

        public ICommand SetPaperColor => setPaperColor ??= new RelayCommand(menuSettingsModel.Execute_SetPaperColor, menuSettingsModel.CanExecute_SetPaperColor);
        RelayCommand setPaperColor;

        public ICommand SetEncoding => setEncoding ??= new RelayCommand(menuSettingsModel.Execute_SetEncoding, menuSettingsModel.CanExecute_SetEncoding);
        RelayCommand setEncoding;

        public ICommand Localization => localization ??= new RelayCommand(menuSettingsModel.Execute_Localization, menuSettingsModel.CanExecute_Localization);
        RelayCommand localization;

        public ICommand Loaded => loaded ??= new RelayCommand(menuSettingsModel.Execute_Loaded, menuSettingsModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(menuSettingsModel.Execute_Close, menuSettingsModel.CanExecute_Close);
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(menuSettingsModel.Execute_Closing, menuSettingsModel.CanExecute_Closing);
        RelayCommand closing;
    }
}
