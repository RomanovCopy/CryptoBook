using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Properties;
using CryptoBook.ViewModels;
using DTO=CryptoBook.DTO;

using System.Collections.ObjectModel;
using CryptoBook.DTO;

namespace CryptoBook.Models
{
    public class SideMenuModel: ViewModelBase
    {

        private readonly ILifetimeScope scope;
        internal ObservableCollection<MenuItemBase> MenuItems { get => menuItems; private set => SetProperty(ref menuItems, value); }
        ObservableCollection<MenuItemBase> menuItems;
        internal ObservableCollection<MenuItem> QuickActions { get => quickActions; private set => SetProperty(ref quickActions, value); }
        ObservableCollection<MenuItem> quickActions;

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
        private readonly IBookmarksViewModel bookmarksViewModel;

        public SideMenuModel(ILifetimeScope _scope)
        {
            scope = _scope;
            menuFileViewModel = _scope.Resolve<IMenuFileViewModel>();
            menuSettingsViewModel = _scope.Resolve<IMenuSettingsViewModel>();
            menuEncryptionViewModel = _scope.Resolve<IMenuEncryptionViewModel>();
            menuContentViewModel = _scope.Resolve<IMenuContentViewModel>();
            bookmarksViewModel = _scope.Resolve<IBookmarksViewModel>();
            Width = Properties.Settings.Default.SideMenuWidth;
            FontSizeHeader = Properties.Settings.Default.SideMenuFontSizeHeader;
            FontSize = Properties.Settings.Default.SideMenuFontSize;
            QuickActions = InitializeQuickActions();
            MenuItems = InitializeMenu();
        }

        private ObservableCollection<MenuItem> InitializeQuickActions()
        {
            var commandService = scope.Resolve<ICommandService>();

            return
            [
                CreateItem(
                    commandService,
                    "Создать",
                    "\uE710",
                    "Создать новую книгу",
                    CommandKey.menuFile_NewFile),
                CreateItem(
                    commandService,
                    "Открыть",
                    "\uE8E5",
                    "Открыть книгу или рабочую директорию",
                    CommandKey.menuFile_OpenFile),
                CreateItem(
                    commandService,
                    "Сохранить",
                    "\uE74E",
                    "Сохранить текущий документ",
                    CommandKey.menuFile_SaveFile)
            ];
        }

        private ObservableCollection<MenuItemBase> InitializeMenu()
        {
            var commandService = scope.Resolve<ICommandService>();
            var file = new MenuItemBase(commandService)
            {
                Name = "Файл",
                IsEnabled = true,
                HasChildren = true
            };
            file.Children.Add(CreateItem(
                commandService,
                "Сохранить",
                "\uE74E",
                "Сохранить изменения в текущем файле",
                CommandKey.menuFile_SaveFile));
            file.Children.Add(CreateItem(
                commandService,
                "Сохранить как",
                "\uE792",
                "Выбрать имя, папку и формат",
                CommandKey.menuFile_SaveAsFile));

            var content = new MenuItemBase(commandService)
            {
                Name = "Содержимое",
                IsEnabled = true,
                HasChildren = true
            };

            content.Children.Add(new MenuItem(commandService)
            {
                Name = "Закладки",
                Glyph = "\uE8A4",
                Description = "Переходы, заметки и ссылки внутри книги",
                IsEnabled = true,
                Command = bookmarksViewModel.OpenManager
            });
            content.Children.Add(CreateItem(
                commandService,
                "Вставить изображение",
                "\uE91B",
                "Добавить изображение в позицию курсора",
                CommandKey.menuContent_InsertImage));
            content.Children.Add(CreateItem(
                commandService,
                "Фото и видео",
                "\uE714",
                "Просмотр медиафайлов рабочей директории",
                CommandKey.menuContent_MediaPlayer));

            return [file, content];
        }

        private static MenuItem CreateItem(
            ICommandService commandService,
            string name,
            string glyph,
            string description,
            CommandKey commandKey)
        {
            return new MenuItem(commandService)
            {
                Name = name,
                Glyph = glyph,
                Description = description,
                IsEnabled = true,
                Command = commandService.GetCommand(commandKey)
                    ?? throw new InvalidOperationException(
                        $"Команда {commandKey} не зарегистрирована.")
            };
        }

        internal bool CanExecute_Lifecycle(object? obj) => true;

        internal void Execute_Loaded(object? obj)
        {
        }

        internal void Execute_Close(object? obj)
        {
        }

        internal void Execute_Closing(object? obj)
        {
            Settings.Default.SideMenuWidth = Width;
            Settings.Default.SideMenuFontSizeHeader = FontSizeHeader;
            Settings.Default.SideMenuFontSize = FontSize;
            Settings.Default.Save();
        }

        internal void Execute_Closed(object? obj)
        {
        }

    }


}
