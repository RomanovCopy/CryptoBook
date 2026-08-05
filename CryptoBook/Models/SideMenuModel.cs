using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Properties;
using CryptoBook.ViewModels;
using DTO=CryptoBook.DTO;

using System.Collections.ObjectModel;
using System.IO;
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
        private readonly IDocumentSession documentSession;
        private readonly IFileManagerService fileManagerService;
        private readonly ITextInputService textInputService;
        private readonly IMessageService messageService;
        private readonly IPageNavigationService pageNavigationService;
        private readonly AsyncRelayCommand renameBookCommand;

        public SideMenuModel(ILifetimeScope _scope)
        {
            scope = _scope;
            menuFileViewModel = _scope.Resolve<IMenuFileViewModel>();
            menuSettingsViewModel = _scope.Resolve<IMenuSettingsViewModel>();
            menuEncryptionViewModel = _scope.Resolve<IMenuEncryptionViewModel>();
            menuContentViewModel = _scope.Resolve<IMenuContentViewModel>();
            bookmarksViewModel = _scope.Resolve<IBookmarksViewModel>();
            documentSession = _scope.Resolve<IDocumentSession>();
            fileManagerService = _scope.Resolve<IFileManagerService>();
            textInputService = _scope.Resolve<ITextInputService>();
            messageService = _scope.Resolve<IMessageService>();
            pageNavigationService = _scope.Resolve<IPageNavigationService>();
            renameBookCommand = new AsyncRelayCommand(RenameBookAsync);
            Width = Properties.Settings.Default.SideMenuWidth;
            FontSizeHeader = Properties.Settings.Default.SideMenuFontSizeHeader;
            FontSize = Properties.Settings.Default.SideMenuFontSize;
            QuickActions = InitializeQuickActions();
            MenuItems = InitializeMenu();
            LocalizationManager.CultureChanged += OnCultureChanged;
        }

        private ObservableCollection<MenuItem> InitializeQuickActions()
        {
            var commandService = scope.Resolve<ICommandService>();

            return
            [
                CreateItem(
                    commandService,
                    LocalizationManager.GetString("SideMenu.Create"),
                    "\uE710",
                    LocalizationManager.GetString("SideMenu.Create.Description"),
                    CommandKey.menuFile_NewFile),
                CreateItem(
                    commandService,
                    LocalizationManager.GetString("Common.Open"),
                    "\uE8E5",
                    LocalizationManager.GetString("SideMenu.Open.Description"),
                    CommandKey.menuFile_OpenFile),
                CreateItem(
                    commandService,
                    LocalizationManager.GetString("Common.Save"),
                    "\uE74E",
                    LocalizationManager.GetString("SideMenu.Save.Description"),
                    CommandKey.menuFile_SaveFile),
                CreateItem(
                    commandService,
                    LocalizationManager.GetString("Common.Close"),
                    "\uE8BB",
                    LocalizationManager.GetString("SideMenu.CloseDocument.Description"),
                    CommandKey.menuFile_CloseFile)
            ];
        }

        private ObservableCollection<MenuItemBase> InitializeMenu()
        {
            var commandService = scope.Resolve<ICommandService>();
            var workspace = new MenuItemBase(commandService)
            {
                Name = LocalizationManager.GetString("SideMenu.Workspace"),
                IsEnabled = true,
                HasChildren = true
            };
            workspace.Children.Add(CreateNavigationItem(
                commandService,
                LocalizationManager.GetString("SideMenu.Editor"),
                "\uE70F",
                LocalizationManager.GetString("SideMenu.Editor.Description"),
                "Home"));
            workspace.Children.Add(CreateNavigationItem(
                commandService,
                LocalizationManager.GetString("SideMenu.SearchDocuments"),
                "\uE721",
                LocalizationManager.GetString(
                    "SideMenu.SearchDocuments.Description"),
                "WorkspaceSearch"));

            var file = new MenuItemBase(commandService)
            {
                Name = LocalizationManager.GetString("Common.File"),
                IsEnabled = true,
                HasChildren = true
            };
            file.Children.Add(CreateItem(
                commandService,
                LocalizationManager.GetString("Common.Save"),
                "\uE74E",
                LocalizationManager.GetString("SideMenu.SaveChanges.Description"),
                CommandKey.menuFile_SaveFile));
            file.Children.Add(CreateItem(
                commandService,
                LocalizationManager.GetString("Common.SaveAs"),
                "\uE792",
                LocalizationManager.GetString("SideMenu.SaveAs.Description"),
                CommandKey.menuFile_SaveAsFile));
            file.Children.Add(new MenuItem(commandService)
            {
                Name = LocalizationManager.GetString("SideMenu.RenameBook"),
                Glyph = "\uE8AC",
                Description = LocalizationManager.GetString("SideMenu.RenameBook.Description"),
                IsEnabled = true,
                Command = renameBookCommand
            });
            var content = new MenuItemBase(commandService)
            {
                Name = LocalizationManager.GetString("SideMenu.Content"),
                IsEnabled = true,
                HasChildren = true
            };

            content.Children.Add(new MenuItem(commandService)
            {
                Name = LocalizationManager.GetString("Bookmarks.Title"),
                Glyph = "\uE8A4",
                Description = LocalizationManager.GetString("SideMenu.Bookmarks.Description"),
                IsEnabled = true,
                Command = bookmarksViewModel.OpenManager
            });
            content.Children.Add(CreateItem(
                commandService,
                LocalizationManager.GetString("SideMenu.InsertImage"),
                "\uE91B",
                LocalizationManager.GetString("SideMenu.InsertImage.Description"),
                CommandKey.menuContent_InsertImage));
            content.Children.Add(CreateItem(
                commandService,
                LocalizationManager.GetString("Media.Title"),
                "\uE714",
                LocalizationManager.GetString("SideMenu.Media.Description"),
                CommandKey.menuContent_MediaPlayer));

            return [workspace, file, content];
        }

        private MenuItem CreateNavigationItem(
            ICommandService commandService,
            string name,
            string glyph,
            string description,
            string pageKey) =>
            new(commandService)
            {
                Name = name,
                Glyph = glyph,
                Description = description,
                IsEnabled = true,
                Command = new RelayCommand(
                    _ => pageNavigationService.Navigate(pageKey),
                    _ => !string.Equals(
                        pageNavigationService.CurrentKey,
                        pageKey,
                        StringComparison.Ordinal))
            };

        private async Task RenameBookAsync(
            object? parameter,
            CancellationToken cancellationToken)
        {
            string? oldPath = documentSession.FilePath;
            string oldFileName = string.IsNullOrWhiteSpace(oldPath)
                ? documentSession.DisplayName
                : Path.GetFileName(oldPath);
            string extension = Path.GetExtension(oldFileName);
            if(string.IsNullOrWhiteSpace(extension))
                extension = documentSession.Template?.DefaultExtension ?? string.Empty;
            string initialName = Path.GetFileNameWithoutExtension(oldFileName);
            string? requestedName = textInputService.Request(
                LocalizationManager.GetString("SideMenu.RenameBook.DialogTitle"),
                LocalizationManager.GetString("SideMenu.RenameBook.Prompt"),
                initialName,
                LocalizationManager.GetString("Common.Rename"));

            if(requestedName is null)
                return;

            string newName = requestedName.Trim();
            if(string.IsNullOrWhiteSpace(newName))
            {
                await messageService.ShowMessage(
                    LocalizationManager.GetString("SideMenu.RenameBook.DialogTitle"),
                    LocalizationManager.GetString("SideMenu.RenameBook.Empty"));
                return;
            }

            if(newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
               newName is "." or "..")
            {
                await messageService.ShowMessage(
                    LocalizationManager.GetString("SideMenu.RenameBook.DialogTitle"),
                    LocalizationManager.GetString("SideMenu.RenameBook.Invalid"));
                return;
            }

            if(newName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                newName = Path.GetFileNameWithoutExtension(newName);

            string newFileName = newName + extension;
            if(string.Equals(
                oldFileName,
                newFileName,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if(string.IsNullOrWhiteSpace(oldPath))
            {
                documentSession.SetDisplayName(newFileName);
                return;
            }

            FileOperationResult result = await fileManagerService.RenameAsync(
                oldPath,
                newFileName,
                cancellationToken);
            if(!result.Success)
            {
                await messageService.ShowMessage(
                    LocalizationManager.GetString("SideMenu.RenameBook.ErrorTitle"),
                    result.ErrorMessage ?? LocalizationManager.GetString(
                        "SideMenu.RenameBook.Failed"));
                return;
            }

            string? directory = Path.GetDirectoryName(oldPath);
            if(string.IsNullOrWhiteSpace(directory))
                return;

            documentSession.Rename(Path.Combine(directory, newFileName));
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
            LocalizationManager.CultureChanged -= OnCultureChanged;
        }

        private void OnCultureChanged(object? sender, EventArgs args)
        {
            QuickActions = InitializeQuickActions();
            MenuItems = InitializeMenu();
        }

    }


}
