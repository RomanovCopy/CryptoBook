using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Infrastructure
{
    public class ResourceWrapper: ViewModelBase
    {
        //public event PropertyChangedEventHandler? PropertyChanged;

        public string? this[string key] => Properties.Resources.ResourceManager.GetString(key);

        //public void OnCultureChanged()
        //{
        //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        //}
    }

}
