using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;
using CryptoBook.Properties;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    public class SideMenuModel: ViewModelBase
    {

        private readonly ILifetimeScope scope;
        internal ObservableCollection<MenuItemViewModel> MenuItems { get => menuItems; private set => SetProperty(ref menuItems, value); }
        ObservableCollection<MenuItemViewModel> menuItems;

        /// <summary>
        /// ширина бокового меню в процентах от ширины окна
        /// </summary>
        internal double Width { get => width; set => SetProperty(ref width, value); }
        private double width;
        /// <summary>
        /// высота шрифта заголовков в процентах от вертикального разрешения экрана
        /// </summary>
        internal double FontSizeHeader { get => fontSizeHeader; set => SetProperty(ref fontSizeHeader, value); }
        double fontSizeHeader;
        /// <summary>
        /// высота шрифта в процентах от вертикального разрешения экрана
        /// </summary>
        internal double FontSize { get => fontSize; set => SetProperty(ref fontSize, value); }
        double fontSize;



        private readonly IMenuFileViewModel menuFileViewModel;
        private readonly IMenuSettingsViewModel menuSettingsViewModel;
        private readonly IMenuEncryptionViewModel menuEncryptionViewModel;
        private readonly IMenuContentViewModel menuContentViewModel;

        public SideMenuModel(ILifetimeScope _scope)
        {
            scope = _scope;
            menuFileViewModel = _scope.Resolve<IMenuFileViewModel>();
            menuSettingsViewModel = Locators.ViewModels.MenuSettingsViewModel;
            menuEncryptionViewModel = Locators.ViewModels.MenuEncryptionViewModel;
            menuContentViewModel = Locators.ViewModels.MenuContentViewModel;
            Width = Properties.Settings.Default.SideMenuWidth;
            FontSizeHeader = Properties.Settings.Default.SideMenuFontSizeHeader;
            FontSize = Properties.Settings.Default.SideMenuFontSize;
            MenuItems = InitializeMenu();
        }


        private ObservableCollection<MenuItemViewModel> InitializeMenu()
        {
            var MenuItems = new ObservableCollection<MenuItemViewModel>

        {
            new() {
                Name =Resources.file,
                Icon = "",
                IsParrent=true,
                IsEnabled=true,
                FontSize=this.FontSizeHeader,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = Resources.newFile,
                        Icon = "📄",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.NewFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.NewFile
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.openFile,
                        Icon = "📝",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.OpenFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.OpenFile
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.openDirectory,
                        Icon = "📂",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.SaveFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.SaveFile
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.saveFile,
                        Icon = "💾",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.SaveFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.SaveFile,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.saveFileAs___,
                        Icon = "🗂️",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.SaveAsFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.SaveAsFile
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.updateFile,
                        Icon = "🔄",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.UpdateFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.UpdateFile
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.synchronization,
                        Icon = "♻️",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.WorkingDirectorySynchronization.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.WorkingDirectorySynchronization,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.closeFile,
                        Icon = "❌",
                        IsParrent=false,
                        IsEnabled=menuFileViewModel.CloseFile.CanExecute(null),
                        FontSize=this.FontSize,
                        SelectItem=menuFileViewModel.CloseFile,
                    },
                }
            },//File
            new() {
                Name=Resources.settings,
                Icon = "",
                IsParrent=true,
                IsEnabled=true,
                FontSize=this.FontSizeHeader,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = Resources.ink,
                        Icon = "🖋️",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuSettingsViewModel.SetFontColor.CanExecute(null),
                        SelectItem=menuSettingsViewModel.SetFontColor,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.background,
                        Icon = "🎨",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuSettingsViewModel.SetFontBackColor.CanExecute(null),
                        SelectItem=menuSettingsViewModel.SetFontBackColor
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.paper,
                        Icon = "📃",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuSettingsViewModel.SetPaperColor.CanExecute(null),
                        SelectItem=menuSettingsViewModel.SetPaperColor
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.encoding,
                        Icon = "🔤",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuSettingsViewModel.SetEncoding.CanExecute(null),
                        SelectItem=menuSettingsViewModel.SetEncoding
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.localization,
                        Icon = "🌐",
                        IsParrent=true,
                        FontSize=this.FontSize,
                        IsEnabled=true,
                        Children =
                        {
                            new MenuItemViewModel
                            {
                                Name = "English",
                                Icon = "",
                                IsParrent=false,
                                FontSize=this.FontSize,
                                IsEnabled=menuSettingsViewModel.Localization.CanExecute("en-EN"),
                                SelectItem=new RelayCommand(menuSettingsViewModel.Localization.Execute, null , "en-EN"),
                            },
                            new MenuItemViewModel
                            {
                                Name = "Русский",
                                Icon = "",
                                IsParrent=false,
                                FontSize=this.FontSize,
                                IsEnabled=menuSettingsViewModel.Localization.CanExecute("ru-RU"),
                                SelectItem=new RelayCommand(menuSettingsViewModel.Localization.Execute, null , "ru-RU")
                            }
                        }
                    },

                }

            },//Settings
            new()
            {
                Name=Resources.encryption,
                Icon = "",
                IsParrent=true,
                IsEnabled=true,
                FontSize=this.FontSizeHeader,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = Resources.encryption__On,
                        Icon = "🔒",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuEncryptionViewModel.InstalKey.CanExecute(null),
                        SelectItem=menuEncryptionViewModel.InstalKey,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.encryption__Off,
                        Icon = "🔓",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuEncryptionViewModel.DeleteKey.CanExecute(null),
                        SelectItem=menuEncryptionViewModel.DeleteKey,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.encrypt,
                        Icon = "🧩",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuEncryptionViewModel.Encrypt.CanExecute(null),
                        SelectItem=menuEncryptionViewModel.Encrypt,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.decrypt,
                        Icon = "🧩",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuEncryptionViewModel.Decrypt.CanExecute(null),
                        SelectItem=menuEncryptionViewModel.Decrypt,
                    },
                }
            },//Encryption
            new()
            {
                Name=Resources.content,
                Icon = "",
                IsParrent=true,
                IsEnabled=true,
                FontSize=this.FontSizeHeader,
                Children =
                {
                    new MenuItemViewModel
                    {
                        Name = Resources.addImage,
                        Icon = "🖼️",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuContentViewModel.InsertImage.CanExecute(null),
                        SelectItem=menuContentViewModel.InsertImage,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.addText,
                        Icon = "📝",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuContentViewModel.InsertText.CanExecute(null),
                        SelectItem=menuContentViewModel.InsertText,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.documentTree,
                        Icon = "📑",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuContentViewModel.OpenDocumentTree.CanExecute(null),
                        SelectItem=menuContentViewModel.OpenDocumentTree,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.readDocument,
                        Icon = "📖",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuContentViewModel.Reading.CanExecute(null),
                        SelectItem=menuContentViewModel.Reading,
                    },
                    new MenuItemViewModel
                    {
                        Name = Resources.mediaPlayer,
                        Icon = "🎥",
                        IsParrent=false,
                        FontSize=this.FontSize,
                        IsEnabled=menuContentViewModel.MediaPlayer.CanExecute(null),
                        SelectItem=menuContentViewModel.MediaPlayer,
                    },

                }
            },//Content
        };
            return MenuItems;
        }

    }


}
