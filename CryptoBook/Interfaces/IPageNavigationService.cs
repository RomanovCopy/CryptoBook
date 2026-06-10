using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CryptoBook.Interfaces
{
    public interface IPageNavigationService: INotifyPropertyChanged,IService
    {
        Page? CurrentPage { get; }
        string? CurrentKey { get; }

        bool CanGoBack { get; }
        bool CanGoForward { get; }

        public IReadOnlyList<string>? Keys { get; }


        void Navigate(string key, object? args = null);
        void GoBack();
        void GoForward();
        void Remove(string key);
    }
}
