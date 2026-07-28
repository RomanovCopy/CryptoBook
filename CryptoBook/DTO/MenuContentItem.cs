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
            Name = "Медиа";
            Children.Add(new MenuItem(commandService)
            {
                Name = "   Фото и видео",
                IsEnabled = true,
                Command = commandService.GetCommand(CommandKey.menuContent_MediaPlayer)
                    ?? throw new InvalidOperationException("Команда MediaPlayer не зарегистрирована.")
            });
        }
    }
}
