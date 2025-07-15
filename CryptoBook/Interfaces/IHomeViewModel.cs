namespace CryptoBook.Interfaces
{
    public interface IHomeViewModel: IPageViewModel
    {
        public Action<object> BehaviorReady { get; set; }
    }
}
