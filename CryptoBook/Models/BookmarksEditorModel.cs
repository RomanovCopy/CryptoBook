using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.Models
{
    internal class BookmarksEditorModel: ViewModelBase
    {
        private readonly IWindowManager windowManager;

        internal double Width { get => width; set => SetProperty(ref width,value); }
        double width;
        internal double Height { get => height; set => SetProperty(ref height,value); }
        double height;
        internal double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        internal double WindowLeft { get => windowLeft; set => SetProperty(ref windowLeft,value); }
        double windowLeft;
        internal WindowState WindowState { get => windowState; set => SetProperty(ref windowState, value); }
        WindowState windowState;
        internal Guid WindowId { get=>windowId; private set=>windowId=value; }
        Guid windowId;





        internal BookmarksEditorModel(IWindowManager manager)
        {
            windowManager= manager;
            WindowId = Guid.NewGuid();
            //восстанавливаем размеры и позицию окна
            Width = Properties.Settings.Default.BookmarksEditor_Width;
            Height = Properties.Settings.Default.BookmarksEditor_Height;
            WindowTop = Properties.Settings.Default.BookmarksEditor_Top;
            WindowLeft = Properties.Settings.Default.BookmarksEditor_Left;
            //восстанавливаем состояние окна
            var state = Properties.Settings.Default.BookmarksEditor_State;
            WindowState = state == "Normal" ? WindowState.Normal : state == "Minimized" ? WindowState.Minimized : state == "Maximized" ? WindowState.Maximized : WindowState.Minimized;
        }


        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
        }



        internal bool CanExecute_Close(object? obj)
        {
            return windowManager is not null && WindowId != Guid.Empty;
        }
        internal void Execute_Close(object? obj)
        {
            windowManager.CloseWindow<BookmarksEditor>(windowId);
        }


        internal bool CanExecute_Closing(object? obj)
        {
            return windowManager is not null && WindowId != Guid.Empty;
        }
        internal void Execute_Closing(object? obj)
        {
            //размеры и положение окна
            if(WindowState.ToString() == "Normal")
            {
                Properties.Settings.Default.BookmarksEditor_Width = Width;
                Properties.Settings.Default.BookmarksEditor_Height = Height;
                Properties.Settings.Default.BookmarksEditor_Left = WindowLeft;
                Properties.Settings.Default.BookmarksEditor_Top = WindowTop;
            }
            Properties.Settings.Default.WindowState = WindowState.ToString();
            Properties.Settings.Default.Save();
        }


        internal bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
    }
}
