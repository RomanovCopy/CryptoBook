namespace CryptoBook.Infrastructure
{
    public class ResourceWrapper: ViewModelBase
    {
        //public event PropertyChangedEventHandler? PropertyChanged;

        public string? this[string key] => Properties.Resources.ResourceManager.GetString(key);

        //public void OnCultureChanged()
        //{
        //    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        //}
    }

}
