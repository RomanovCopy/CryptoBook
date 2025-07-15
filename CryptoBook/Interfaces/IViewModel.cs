using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IViewModel
    {

        public static Action Ready { get; set; }
        /// <summary>
        /// загружено
        /// </summary>
        public ICommand Loaded { get; }
        /// <summary>
        /// закрыть
        /// </summary>
        public ICommand Close { get; }
        /// <summary>
        /// перед закрытием
        /// </summary>
        public ICommand Closing { get; }
        /// <summary>
        /// после закрытия
        /// </summary>
        public ICommand Closed { get; }
    }
}
