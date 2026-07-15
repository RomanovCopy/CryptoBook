using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class EncryptionMode_ViewModel: ViewModelBase, IEncryptionMode_ViewModel
    {

        private readonly IEncryptionMode_Model _model;

        public Guid WindowId => _model.WindowId;

        public double WindowWidth { get => _model.WindowWidth; set => _model.WindowWidth = value; }
        public double WindowHeight { get => _model.WindowHeight; set => _model.WindowHeight = value; }
        public double WindowTop { get => _model.WindowTop; set => _model.WindowTop = value; }
        public double WindowLeft { get => _model.WindowLeft; set => _model.WindowLeft = value; }
        public WindowState WindowState { get => _model.WindowState; set => _model.WindowState = value; }
        public EncryptionTargetMode SelectedMode { get => _model.SelectedMode; set => _model.SelectedMode = value; }
        public string Title { get => _model.Title; }
        public string MessageMode { get => _model.MessageMode; }
        public string MessageModeTop { get => _model.MessageModeTop; }
        public string MessageModeBottom { get => _model.MessageModeBottom; }
        public string Path { get => _model.Path; }
        public string WarningMessage { get => _model.WarningMessage; }


        public EncryptionMode_ViewModel(IEncryptionMode_Model model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }




        public ICommand Loaded => _loaded ??= new RelayCommand(_model.Execute_Loaded, _model.CanExecute_Loaded);
        RelayCommand? _loaded;

        public ICommand Close => _close ??= new RelayCommand(_model.Execute_Close, _model.CanExecute_Close);
        RelayCommand? _close;

        public ICommand Closing => _closing ??= new RelayCommand(_model.Execute_Closing, _model.CanExecute_Closing);
        RelayCommand? _closing;

        public ICommand Closed => _closed ??= new RelayCommand(_model.Execute_Closed, _model.CanExecute_Closed);
        RelayCommand? _closed;

    }
}
