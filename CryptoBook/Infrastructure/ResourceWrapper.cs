namespace CryptoBook.Infrastructure
{
    public class ResourceWrapper: ViewModelBase
    {
        private static readonly List<WeakReference<ResourceWrapper>> Instances = [];

        public ResourceWrapper()
        {
            lock(Instances)
                Instances.Add(new WeakReference<ResourceWrapper>(this));
        }

        public string this[string key] => LocalizationManager.GetString(key);

        public static void NotifyCultureChanged()
        {
            lock(Instances)
            {
                for(int index = Instances.Count - 1; index >= 0; index--)
                {
                    if(Instances[index].TryGetTarget(out ResourceWrapper? wrapper))
                        wrapper.OnPropertyChanged("Item[]");
                    else
                        Instances.RemoveAt(index);
                }
            }
        }
    }
}
