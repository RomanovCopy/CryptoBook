using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class TreeNodeDirectory:DirectoryContent
    {
        public bool IsExpanded { get => _isExpanded; set
            {
                if(SetProperty(ref _isExpanded, value) && value)
                {
                    _ = EnsureLoadedAsync();
                }
            } 
        }
        bool _isExpanded;
        public bool IsLoaded { get => _isLoaded; private set => SetProperty(ref _isLoaded, value); }
        bool _isLoaded;
        public ObservableCollection<TreeNodeDirectory> ChildDirectories { get => _childDirectories; set => SetProperty(ref _childDirectories, value); }
        ObservableCollection<TreeNodeDirectory> _childDirectories;

        public TreeNodeDirectory(string fullPath) : base()
        {
            base.DirectoryPath = fullPath;
            ChildDirectories = [];
        }   

        public async Task EnsureLoadedAsync()
        {
            if(IsLoaded)
                return;

            // загрузка каталогов
            foreach(var dir in Directory.EnumerateDirectories(DirectoryPath))
            {
                _childDirectories.Add(new TreeNodeDirectory(dir));
            }

            IsLoaded = true;
        }

    }
}
