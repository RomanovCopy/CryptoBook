using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class BookmarksEditorViewModel: ViewModelBase, IBookmarksEditorViewModel
    {
        private readonly BookmarksEditorModel model;

        public event EventHandler RequestClose;

        public Guid WindowId => model.WindowId;

        public double Width { get => model.Width; set => model.Width=value; }
        public double Height { get => model.Height; set => model.Height=value; }
        public double WindowTop { get =>model.WindowTop; set => model.WindowTop=value; }
        public double WindowLeft { get => model.WindowLeft; set => model.WindowLeft=value; }
        public WindowState WindowState { get => model.WindowState; set => model.WindowState=value; }




        public BookmarksEditorViewModel(IWindowManager manager)
        {
            model = new BookmarksEditorModel(manager);
            model.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand Loaded => loaded ??= new RelayCommand(model.Execute_Loaded, model.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(model.Execute_Close, model.CanExecute_Close); 
        RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(model.Execute_Closing, model.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(model.Execute_Closed, model.CanExecute_Closed);
        RelayCommand closed;

    }
}
