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
    public class TimeWebAIControlViewModel: ViewModelBase, ITimeWebAIControlViewModel
    {
        private readonly ITimeWebAIControlModel model;

        public string Url { get => model.Url; set => model.Url = value; }


        public TimeWebAIControlViewModel(ITimeWebAIControlModel model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName ?? string.Empty);
        }



        public ICommand Loaded => loaded ??= new RelayCommand(model.Execute_Loaded, model.CanExecute_Loaded);
        RelayCommand? loaded;

        public ICommand Close => close ??= new RelayCommand(model.Execute_Close, model.CanExecute_Close);
        RelayCommand? close;

        public ICommand Closing => closing ??= new RelayCommand(model.Execute_Closing, model.CanExecute_Closing);
        RelayCommand? closing;

        public ICommand Closed => closed ??= new RelayCommand(model.Execute_Closed, model.CanExecute_Closed);
        RelayCommand? closed;

        public ICommand NewAgent => throw new NotImplementedException();
    }
}
