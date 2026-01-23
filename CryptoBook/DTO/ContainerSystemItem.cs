using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public abstract class ContainerSystemItem:ViewModelBase,IContainerSystemItem
    {

        public string RootDirectory { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string FullPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DateTime LastWriteTimeUtc { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsLoaded { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsExpanded { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ReadOnlyObservableCollection<ISystemItem> Children => throw new NotImplementedException();
        protected ContainerSystemItem()
        {
        }

        public virtual FileOperationResult AddChild(ISystemItem item)
        {
            throw new NotImplementedException();
        }

        public virtual FileOperationResult RemoveChild(ISystemItem item)
        {
            throw new NotImplementedException();
        }

        public virtual FileOperationResult ClearChildren()
        {
            throw new NotImplementedException();
        }

        public virtual void SyncCollections<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector)
        {
            throw new NotImplementedException();
        }

    }
}
