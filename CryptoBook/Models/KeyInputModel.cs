using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    public class KeyInputModel:ViewModelBase, IKeyInputModel

    {
        public Guid WindowId { get => windowId; private set => SetProperty(ref windowId, value); }
        Guid windowId;


        public double WindowWidth { get => windowWidth; set => SetProperty(ref windowWidth, value); }
        double windowWidth;
        public double WindowHeight { get => windowHeight; set => SetProperty(ref windowHeight, value); }
        double windowHeight;
        public double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        public double WindowLeft { get => windowLeft; set => SetProperty(ref windowLeft, value); }
        double windowLeft;
        public WindowState WindowState { get => windowState; set => SetProperty(ref windowState, value); }
        WindowState windowState;


        public string Title { get => title; private set => SetProperty(ref title, value); }
        string title;

        public string Message { get => message; private set => SetProperty(ref message, value); }
        string message;

        public bool ShowRepeatPassword { get => showRepeatPassword; init => SetProperty(ref showRepeatPassword, value); }
        bool showRepeatPassword;


        private readonly IWindowManager _windowManager;

        public KeyInputModel(IWindowManager windowManager)
        {
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            WindowId = Guid.NewGuid();
            Title = "Ключ шифрования";
            Message = "Введите ключ шифрования:";
            ShowRepeatPassword = true;
        }



        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            WindowHeight = Properties.Settings.Default.KeyInputHeight;
            WindowLeft = Properties.Settings.Default.KeyInputLeft;
            WindowTop = Properties.Settings.Default.KeyInputTop;
            WindowWidth = Properties.Settings.Default.KeyInputWidth;
            WindowState = Properties.Settings.Default.KeyInputState;
        }


        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
            Properties.Settings.Default.KeyInputHeight = WindowHeight;
            Properties.Settings.Default.KeyInputLeft = WindowLeft;
            Properties.Settings.Default.KeyInputTop = WindowTop;
            Properties.Settings.Default.KeyInputWidth = WindowWidth;
            Properties.Settings.Default.KeyInputState = WindowState;
            Properties.Settings.Default.Save();
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

    }
}
