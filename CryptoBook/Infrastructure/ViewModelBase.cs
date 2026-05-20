using CryptoBook.DTO;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CryptoBook.Infrastructure
{

    [Serializable]
    public class ViewModelBase: INotifyPropertyChanged
    {
        /// <summary>
        /// автоматическое определение типа Item
        /// </summary>
        public ItemKind Kind => this is IDriveItem ? ItemKind.Drive : this is IDirectoryItem ? ItemKind.Directory : this is IFileItem ? ItemKind.File : ItemKind.None; 
        
        protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if(EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        protected virtual bool SetProperty<T>(ref T field, T value, params string[] names)
        {
            if(EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(names);
            return true;
        }

        [field: NonSerialized]
        public virtual event PropertyChangedEventHandler? PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public virtual void OnPropertyChanged(params string[] names)
        {
            foreach(var name in names)
            {
                OnPropertyChanged(name);
            }
        }

        public void ErrorWindow(Exception e, [CallerMemberName] string name = "")
        {
            var mytype = GetType().ToString().Split('.').LastOrDefault();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(e.Message, $"{mytype}.{name}");
            });
        }
    }
}
