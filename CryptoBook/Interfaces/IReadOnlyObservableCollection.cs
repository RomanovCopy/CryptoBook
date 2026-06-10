using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IReadOnlyObservableCollection<out T>: IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }
}
