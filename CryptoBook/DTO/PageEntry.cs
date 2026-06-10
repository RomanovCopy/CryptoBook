using Autofac;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CryptoBook.DTO
{
    public sealed class PageEntry
    {
        public string Key { get; }
        public Page Page { get; }
        public ILifetimeScope Scope { get; }

        public PageEntry(string key, Page page, ILifetimeScope scope)
        {
            Key = key;
            Page = page;
            Scope = scope;
        }

        public void Dispose() => Scope.Dispose();
    }
}
