using Autofac;

using CryptoBook.Converters;
using CryptoBook.DTO;
using CryptoBook.Infrastructure;
using CryptoBook.Injections;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CryptoBook.Services
{
    public sealed class PageNavigationService: ViewModelBase, IPageNavigationService, IDisposable
    {
        private readonly ILifetimeScope _windowScope;

        private readonly List<PageEntry> _list = [];
        private int _index = -1;

        public IReadOnlyList<string>? Keys =>_list?.Select(e=>e.Key).ToList();

        public Page? CurrentPage => _index >= 0 && _index < _list.Count ? _list[_index].Page : null;

        public string? CurrentKey => _index >= 0 && _index < _list.Count ? _list[_index].Key : null;

        public bool CanGoBack => _index > 0;
        public bool CanGoForward => _index < _list.Count - 1;



        public PageNavigationService(ILifetimeScope windowScope)
        {
            _windowScope = windowScope;
        }


        public void Navigate(string key, object? args = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if(args is null)
            {
                int existingIndex = _list.FindIndex(entry =>
                    string.Equals(
                        entry.Key,
                        key,
                        StringComparison.Ordinal));
                if(existingIndex >= 0)
                {
                    if(existingIndex != _index)
                    {
                        _index = existingIndex;
                        NotifyNavigationChanged();
                    }
                    return;
                }
            }

            TrimForward();
            var entry = CreateEntry(key, args);
            _list.Add(entry);
            _index = _list.IndexOf(entry);
            NotifyNavigationChanged();
        }

        public void GoBack()
        {
            if(!CanGoBack)
                return;

            _index--;
            NotifyNavigationChanged();
        }

        public void GoForward()
        {
            if(!CanGoForward)
                return;

            _index++;
            NotifyNavigationChanged();
        }

        public void Remove(string key)
        {
            var idx = _list.FindIndex(e => e.Key == key);
            if(idx < 0)
                return;

            var entry = _list[idx];
            _list.RemoveAt(idx);

            if(idx < _index)
                _index--;
            else if(idx == _index)
                _index = Math.Min(idx, _list.Count - 1);

            NotifyNavigationChanged();
            entry.Dispose();
        }

        private PageEntry CreateEntry(string key, object? args)
        {
            var pageScope = _windowScope.BeginLifetimeScope(b =>
            {
                // Страницы должны управлять навигацией окна, а не получать
                // собственный пустой InstancePerLifetimeScope-экземпляр.
                b.RegisterInstance(this)
                    .As<IPageNavigationService>()
                    .ExternallyOwned();
                if(args is not null)
                    b.RegisterInstance(args)
                     .As(args.GetType())
                     .SingleInstance();
            });

            Page page;

            var registry = pageScope.Resolve<IPageRegistry>();
            var pageType = registry.Resolve(key);


            using(AmbientScope.Push(pageScope))
            {
                page = (Page)pageScope.Resolve(pageType);
            }

            return new PageEntry(key, page, pageScope);
        }

        private void TrimForward()
        {
            if(_index >= _list.Count - 1)
                return;

            var toRemove = _list
                .Skip(_index + 1)
                .ToList();

            foreach(var e in toRemove)
                e.Dispose();

            _list.RemoveRange(_index + 1, _list.Count - _index - 1);
        }

        private void NotifyNavigationChanged() =>
            OnPropertyChanged([
                "CurrentPage",
                "CurrentKey",
                "CanGoBack",
                "CanGoForward",
                "Keys"]);

        public void Dispose()
        {
            foreach(var entry in _list)
                entry.Dispose();

            _list.Clear();
            _index = -1;
        }

    }
}
