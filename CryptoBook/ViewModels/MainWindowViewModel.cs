using CryptoBook.Infrastructure;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.ViewModels
{
    public class MainWindowViewModel:ViewModelBase
    {
        private readonly MainWindowModel mainWindowModel;
        public MainWindowViewModel()
        {
            mainWindowModel = new();
            mainWindowModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }
    }
}
