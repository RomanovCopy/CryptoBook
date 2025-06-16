using Autofac;

using CryptoBook.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace CryptoBook.Markup
{

    /// <summary>
    /// MarkupExtension для разрешения ViewModel из контейнера Autofac с кэшированием.
    /// </summary>
    /// <summary>
    /// MarkupExtension для разрешения ViewModel из контейнера Autofac с кэшированием.
    /// </summary>
    public class ResolveViewModelExtension: MarkupExtension
    {
        private static readonly ConcurrentDictionary<Type, IViewModel> viewModelCache = new();

        /// <summary>
        /// Тип ViewModel, который должен быть разрешен.
        /// </summary>
        public Type ViewModelType { get; set; }

        /// <summary>
        /// Предоставляет экземпляр ViewModel из контейнера Autofac с использованием кэширования.
        /// </summary>
        /// <param name="serviceProvider">Провайдер сервисов.</param>
        /// <returns>Экземпляр ViewModel указанного типа.</returns>
        /// <exception cref="InvalidOperationException">Если ViewModelType не указан, не реализует IViewModel или не найден в контейнере.</exception>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Проверка корректности ViewModelType
            if(ViewModelType == null)
            {
                throw new InvalidOperationException("ViewModelType must be specified.");
            }

            if(!typeof(IViewModel).IsAssignableFrom(ViewModelType))
            {
                throw new InvalidOperationException($"Type {ViewModelType.FullName} must implement IViewModel.");
            }

            // Проверка кэша
            if(viewModelCache.TryGetValue(ViewModelType, out var cachedViewModel))
            {
                // Для диагностики: логируем, что взяли из кэша
                System.Diagnostics.Debug.WriteLine($"Returning cached ViewModel for {ViewModelType.FullName}");
                return cachedViewModel;
            }

            // Получение контейнера Autofac
            if(System.Windows.Application.Current is not IContainerProvider containerProvider ||
                containerProvider.Container is not IContainer container)
            {
                throw new InvalidOperationException("Autofac container not found in Application.Current.");
            }

            // Разрешение ViewModel из контейнера
            object viewModel = container.Resolve(ViewModelType)
                ?? throw new InvalidOperationException($"Type {ViewModelType.FullName} could not be resolved from Autofac container.");

            // Проверка, что разрешенный объект реализует IViewModel
            if(viewModel is not IViewModel resolvedViewModel)
            {
                throw new InvalidOperationException($"Resolved type {ViewModelType.FullName} does not implement IViewModel.");
            }

            // Добавление в кэш
            if(viewModelCache.TryAdd(ViewModelType, resolvedViewModel))
            {
                // Для диагностики: логируем успешное добавление
                System.Diagnostics.Debug.WriteLine($"Added ViewModel for {ViewModelType.FullName} to cache.");
            } else
            {
                // Если добавление не удалось (например, из-за гонки), берем существующий объект
                System.Diagnostics.Debug.WriteLine($"Failed to add ViewModel for {ViewModelType.FullName} to cache; returning existing.");
                viewModelCache.TryGetValue(ViewModelType, out resolvedViewModel);
            }

            return resolvedViewModel;
        }
    }

}
