using Autofac;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Injections
{
    public static class AmbientScope
    {
        private static readonly System.Threading.AsyncLocal<ILifetimeScope?> _current = new();

        public static ILifetimeScope? TryGetCurrent() => _current.Value;

        public static ScopeGuard Push(ILifetimeScope scope) => new(scope);

        public readonly struct ScopeGuard: IDisposable
        {
            private readonly ILifetimeScope? _prev;
            public ScopeGuard(ILifetimeScope scope)
            {
                _prev = _current.Value;
                _current.Value = scope;
            }
            public void Dispose() => _current.Value = _prev;
        }
    }
}
