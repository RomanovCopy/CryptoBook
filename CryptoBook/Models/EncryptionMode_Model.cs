using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Properties;
using CryptoBook.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    public class EncryptionMode_Model: ViewModelBase, IEncryptionMode_Model
    {

        private readonly IWindowManager _windowManager;

        private bool _seslected;

        public Guid WindowId { get; private set; }



        public double WindowWidth { get => _windowWidth; set => SetProperty(ref _windowWidth, value); }
        double _windowWidth;
        public double WindowHeight { get => _windowHeight; set => SetProperty(ref _windowHeight, value); }
        double _windowHeight;
        public double WindowTop { get => _windowTop; set => SetProperty(ref _windowTop, value); }
        double _windowTop;
        public double WindowLeft { get => _windowLeft; set => SetProperty(ref _windowLeft, value); }
        double _windowLeft;
        public WindowState WindowState { get => _windowState; set => SetProperty(ref _windowState, value); }
        WindowState _windowState;

        public EncryptionTargetMode SelectedMode { get => selectedMode; set => SetProperty(ref selectedMode, value); }
        EncryptionTargetMode selectedMode;



        public ISystemItem ProcessedItem { get => _processedItem; private set => SetProperty(ref _processedItem, value); }
        ISystemItem _processedItem;


        public string Title { get => _title; private set => SetProperty(ref _title, value); }
        string _title;

        public string MessageMode { get => _messageMode; private set => SetProperty(ref _messageMode, value); }
        string _messageMode;

        public string MessageModeTop { get => _messageModeTop; private set => SetProperty(ref _messageModeTop, value); }
        string _messageModeTop;

        public string MessageModeBottom { get => _messageModeBottom; private set => SetProperty(ref _messageModeBottom, value); }
        string _messageModeBottom;

        public string Path { get => _path; private set => SetProperty(ref _path, value); }
        string _path;

        public string WarningMessage { get => _warningMessage; private set => SetProperty(ref _warningMessage, value); }
        string _warningMessage;





        public EncryptionMode_Model(IWindowContext context, IWindowManager windowManager)
        {
            WindowId = Guid.NewGuid();
            _windowManager = windowManager;
            if(context is null){ _windowManager.CloseWindow(WindowId); }
            _seslected = false;
            Initialize(context);
        }



        public bool CanExecute_ButtonOk(object? obj)
        {
            return true;
        }

        public void Execute_ButtonOk(object? obj)
        {
            _seslected = true;
            _windowManager.CloseWindow(WindowId);
        }

        public bool CanExecute_ButtonCancel(object? obj)
        {
            return true;
        }

        public void Execute_ButtonCancel(object? obj)
        {
            _seslected = false;
            _windowManager.CloseWindow(WindowId);
        }



        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
        }


        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            SelectedMode = _seslected ? SelectedMode : EncryptionTargetMode.Cancels;
            Settings.Default.EncryptionModeWidth = WindowWidth;
            Settings.Default.EncryptionModeHeight = WindowHeight;
            Settings.Default.EncryptionModeLeft = WindowLeft;
            Settings.Default.EncryptionModeTop = WindowTop;
            Settings.Default.Save();
        }



        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
            _windowManager.CloseWindow(WindowId);
        }



        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        public void Execute_Closed(object? obj)
        {
        }

        private void Initialize(IWindowContext context)
        {
            WindowWidth = Settings.Default.EncryptionModeWidth;
            WindowHeight = Settings.Default.EncryptionModeHeight;
            WindowLeft = Settings.Default.EncryptionModeLeft;
            WindowTop = Settings.Default.EncryptionModeTop;
            Title = "Режим сохранения файла";
            MessageMode = "Выберите способ сохранения файла:";
            MessageModeTop = "Сохранить файл как ...";
            MessageModeBottom = "Заменить исходный файл";
            SelectedMode = EncryptionTargetMode.SaveAs;
            ProcessedItem = GetProcessedItem(context) ?? throw new NotImplementedException();
        }

        private ISystemItem? GetProcessedItem(IWindowContext context)
        {
            if(context.Items is IReadOnlyDictionary<string, object> dict && dict["path"] is ISystemItem item)
            {
                return item;
            }
            return null;
        }

    }
}
