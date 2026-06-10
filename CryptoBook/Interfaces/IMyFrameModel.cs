using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CryptoBook.Interfaces
{
    public interface IMyFrameModel:INotifyPropertyChanged, IModel
    {
        string? CurrentPageKey { get; }
        Page? CurrentPage { get; }

        bool CanExecute_Navigate(object? obj);
        void Execute_Navigate(object? obj);
        bool CanExecute_GoForward(object? obj);
        void Execute_GoForward(object? obj);
        bool CanExecute_GoBack(object? obj);
        void Execute_GoBack(object? obj);
        bool CanExecute_RemovePage(object? obj);
        void Execute_RemovePage(object? obj);   
    }
}
