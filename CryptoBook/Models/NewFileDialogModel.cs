using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class NewFileDialogModel:ViewModelBase,INewFileDialogModel,IWindowWithId
    {
        private readonly IFileTemplateRegistry _registry;
        private readonly IFileCreationService _creator;

        private CancellationTokenSource? _cts;

        public Guid WindowId { get => windowId; private set => SetProperty(ref windowId, value); }
        Guid windowId;

        public IReadOnlyList<IFileTemplate> Templates { get => templates; private set => SetProperty(ref templates, value); }
        IReadOnlyList<IFileTemplate> templates;

        public IFileTemplate? SelectedTemplate { get=>template; set=>SetProperty(ref template, value); }
        IFileTemplate? template;

        public string FileName { get => fileName ?? string.Empty; set => SetProperty(ref fileName, value ?? string.Empty); }
        string fileName;

        public IfExistsMode IfExists { get => ifExist = IfExistsMode.AutoRename; set => SetProperty(ref ifExist, value); } 
        IfExistsMode ifExist;


        public NewFileDialogModel(IFileTemplateRegistry registry, IFileCreationService creator)
        {
            _registry = registry;
            _creator = creator;
            WindowId = Guid.NewGuid();
            Templates = _registry.GetAll();
            SelectedTemplate = Templates.FirstOrDefault();
        }


        public bool CanExceute_InitSuggested(object? obj)
        {
            return SelectedTemplate is not null;
        }
        public void Execute_InitSuggested(object? obj)
        {
            if(obj is string targetDirectory)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _ = InitSuggestedAsync(targetDirectory, ct);
            }
        }


        public bool CanExecute_Create(object? obj)
        {
            return SelectedTemplate is not null && !string.IsNullOrWhiteSpace(FileName);
        }
        public void Execute_Create(object? obj)
        {
            if(obj is string targetDirectory) 
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                var ct = _cts.Token;
                _ = CreateAsync(targetDirectory, ct);
            }
        }

        public bool CanExecute_Cancel(object? obj)
        {
            return _cts is not null;
        }
        public void Execute_Cancel(object? obj)
        {
            _cts?.Cancel();
        }





        public bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }


        public bool CanExecute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Loaded(object? obj)
        {
            throw new NotImplementedException();
        }



        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            _cts?.Dispose();
        }


        public bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
        public void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }


        private async Task InitSuggestedAsync(string targetDirectory, CancellationToken ct)
        {
            if(SelectedTemplate is null)
                return;
            FileName = await _creator.SuggestUniqueNameAsync(targetDirectory, SelectedTemplate, ct);
        }

        private async Task<FileOperationResult> CreateAsync(string targetDirectory, CancellationToken ct)
        {
            if(SelectedTemplate is null)
                return FileOperationResult.Fail("Не выбран тип файла.");

            return await _creator.CreateAsync(targetDirectory, FileName, SelectedTemplate, IfExists, ct);
        }

    }
}
