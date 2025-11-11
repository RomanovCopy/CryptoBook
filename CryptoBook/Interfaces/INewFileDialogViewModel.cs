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

        public string TargetDirectory { get; set; }

        public string FileName { get; set; }

        public string ErrorMessage { get;}
        public bool CanWrite { get; set; }
        public bool ShowHiddenFiles { get; set; }
        public bool CreateDirectoryIfMissing { get; set; }  

        public IfExistsMode IfExists { get; set; }

        public ICommand Browse { get; }
        public ICommand CreateDirectory { get; }
        public ICommand InitSuggested { get; }
        public ICommand Create { get; }
        public ICommand Cancel { get; }
    }
}
