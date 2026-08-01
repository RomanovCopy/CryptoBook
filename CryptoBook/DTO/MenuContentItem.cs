using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

namespace CryptoBook.DTO
{
    public class MenuContentItem: MenuItemBase
    {
        public MenuContentItem(ICommandService commandService) : base(commandService)
        {
            IsEnabled = true;
            Initialize();
            HasChildren = Children.Count > 0;
        }

        protected override void Initialize()
        {
            Name = LocalizationManager.GetString("Media.Title");
            Children.Add(new MenuItem(commandService)
            {
                Name = "   " +
                    LocalizationManager.GetString("Media.EmptyTitle"),
                IsEnabled = true,
                Command = commandService.GetCommand(CommandKey.menuContent_MediaPlayer)
                    ?? throw new InvalidOperationException("Команда MediaPlayer не зарегистрирована.")
            });
        }
    }
}
