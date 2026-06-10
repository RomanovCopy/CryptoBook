using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface ITitleBarModel:IModel
    {

        public double MyFontSize { get; set; }
        public string MyText { get; set; }



        public bool CanExecute_TitleBarDoubleClick(object? obj);
        public void Execute_TitleBarDoubleClick(object? obj);

        public bool CanExecute_MouseLeftButtonDown(object? obj);
        public void Execute_MouseLeftButtonDown(object? obj);

        public bool CanExecute_TitleBarMouseMove(object? obj);
        public void Execute_TitleBarMouseMove(object? obj);

        public bool CanExecute_ButtonBack_Click(object? obj);
        public void Execute_ButtonBack_Click(object? obj);

        public bool CanExecute_ButtonForward_Click(object? obj);
        public void Execute_ButtonForward_Click(object? obj);

        public bool CanExecute_ToggleMenu_Click(object? obj);
        public void Execute_ToggleMenu_Click(object? obj);

        public bool CanExecute_ButtonDarkThemeClick(object? obj);
        public void Execute_ButtonDarkThemeClick(object? obj);


        public bool CanExecute_ButtonSettingsClick(object? obj);
        public void Execute_ButtonSettingsClick(object? obj);

        public bool CanExecute_ButtonLightTheme_Click(object? obj);
        public void Execute_ButtonLightTheme_Click(object? obj);


        public bool CanExecute_MinButtonClick(object? obj);
        public void Execute_MinButtonClick(object? obj);

        public bool CanExecute_MaxButtonClick(object? obj);
        public void Execute_MaxButtonClick(object? obj);

        public bool CanExecute_CloseButtonClick(object? obj);
        public void Execute_CloseButtonClick(object? obj);

        public bool CanExecute_GoToWindow(object? obj);
        public void Execute_GoToWindow(object? obj);


    }
}
