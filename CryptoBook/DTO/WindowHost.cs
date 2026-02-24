using Autofac;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoBook.DTO
{
    public sealed class WindowHost: IDisposable
    {
        public WindowHost(Guid id, ILifetimeScope scope, Window window)
        {
            Key = id;
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Window = window ?? throw new ArgumentNullException(nameof(window));

            Window.Closed += OnClosed;
        }

        public Guid Key { get; }
        public ILifetimeScope Scope { get; }
        public Window Window { get; }

        public bool IsClosed { get; private set; }

        private void OnClosed(object? sender, EventArgs e)
        {
            // Закрытие пользователем (крестик), Alt+F4, Close() и т.п.
            Dispose();
        }

        public void Dispose()
        {
            if(IsClosed)
                return;
            IsClosed = true;


            // Важно: сначала освобождаем окно, потом scope
            // (scope может держать VM, сервисы и т.п.)
            try
            {
                Window.Closed -= OnClosed;
            } catch
            {
                // ignore: окно уже может быть в teardown
            }

            Scope.Dispose();
        }
    }
}
