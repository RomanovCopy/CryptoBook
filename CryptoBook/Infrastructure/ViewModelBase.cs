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
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(e.Message, $"{mytype}.{name}");
            });
        }
    }
}
