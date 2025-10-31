using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface INewFileDialogViewModel:IViewModel,IWindowWithId
    {
        public IReadOnlyList<IFileTemplate> Templates { get; }

        public IFileTemplate? SelectedTemplate { get; set; }

        public string FileName { get; set; }

        public IfExistsMode IfExists { get; set; }


        ICommand InitSuggested { get; }
        ICommand Create { get; }
        ICommand Cancel { get; }
    }
}
