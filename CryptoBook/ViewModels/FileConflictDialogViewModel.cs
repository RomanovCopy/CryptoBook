using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System.IO;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public sealed class FileConflictDialogViewModel:
        ViewModelBase,
        IWindowWithId,
        IDialogResult<FileConflictDecision?>
    {
        private bool _applyToAll;

        public FileConflictDialogViewModel(IWindowContext context)
        {
            SourcePath = context.Get<string>("sourcePath");
            DestinationPath = context.Get<string>("destinationPath");
            bool isDirectory = context.Get<bool>("isDirectory");
            Title = LocalizationManager.GetString("Explorer.Conflict.Title");
            Message = LocalizationManager.Format(
                isDirectory
                    ? "Explorer.Conflict.DirectoryMessage"
                    : "Explorer.Conflict.FileMessage",
                Path.GetFileName(DestinationPath));
        }

        public event EventHandler<bool?>? CloseRequested;

        public Guid WindowId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string Message { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public FileConflictDecision? Result { get; private set; }

        public bool ApplyToAll
        {
            get => _applyToAll;
            set => SetProperty(ref _applyToAll, value);
        }

        public ICommand ReplaceCommand => _replaceCommand ??= new RelayCommand(
            _ => Close(FileConflictAction.Replace, true));
        private RelayCommand? _replaceCommand;

        public ICommand SkipCommand => _skipCommand ??= new RelayCommand(
            _ => Close(FileConflictAction.Skip, true));
        private RelayCommand? _skipCommand;

        public ICommand KeepBothCommand => _keepBothCommand ??= new RelayCommand(
            _ => Close(FileConflictAction.KeepBoth, true));
        private RelayCommand? _keepBothCommand;

        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(
            _ => Close(FileConflictAction.Cancel, false));
        private RelayCommand? _cancelCommand;

        private void Close(FileConflictAction action, bool? dialogResult)
        {
            Result = new FileConflictDecision(action, ApplyToAll);
            CloseRequested?.Invoke(this, dialogResult);
        }
    }
}
