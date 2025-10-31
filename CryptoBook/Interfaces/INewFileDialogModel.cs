using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface INewFileDialogModel:IModel,IWindowWithId
    {
        public IReadOnlyList<IFileTemplate> Templates { get; }

        public IFileTemplate? SelectedTemplate { get; set; }

        public string FileName { get ; set; }

        public IfExistsMode IfExists { get; set; }


        public bool CanExceute_InitSuggested(object? obj);
        public void Execute_InitSuggested(object? obj);

        public bool CanExecute_Create(object? obj);
        public void Execute_Create(object? obj);

        public bool CanExecute_Cancel(object? obj);
        public void Execute_Cancel(object? obj);
    }
}
