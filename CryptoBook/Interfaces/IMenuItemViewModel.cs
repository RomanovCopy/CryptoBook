using System.Windows.Input;


namespace CryptoBook.Interfaces
{
    public interface IMenuItemViewModel
    {
        /// <summary>
        /// выбор элемента
        /// </summary>
        public ICommand SelectItem { get; }

    }
}
