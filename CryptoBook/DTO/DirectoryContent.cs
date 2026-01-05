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
    public class DirectoryContent:ViewModelBase
    {
        public string DirectoryPath { get => directoryPath; set => SetProperty(ref directoryPath,value); }
        string directoryPath;
        public bool IsHidden { get => isHidden; set => SetProperty(ref isHidden, value); }
        bool isHidden;
        public ObservableCollection<FileItem> Items { get => items; }
        private readonly ObservableCollection<FileItem> items;
    }
}
