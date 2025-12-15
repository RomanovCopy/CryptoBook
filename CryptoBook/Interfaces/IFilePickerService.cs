using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    /// <summary>
    /// Сервис для асинхронного выбора файла пользователем.
    /// Реализации показывают UI-диалог (или эквивалент) и возвращают путь к выбранному файлу.
    /// </summary>
    public interface IFilePickerService
    {
        /// <summary>
        /// Асинхронно открывает диалог выбора файла и возвращает путь к выбранному файлу.
        /// </summary>
        /// <param name="initialDirectory">
        /// Начальная директория, отображаемая в диалоге. Может быть <c>null</c>, тогда используется директория по умолчанию.
        /// </param>
        /// <param name="ct">
        /// Токен отмены, позволяющий прервать операцию. При отмене реализация должна корректно завершить работу;
        /// возможно выбрасывание <see cref="OperationCanceledException"/> в случае принудительной отмены.
        /// </param>
        /// <returns>
        /// Задача, результатом которой является путь к выбранной папке в виде строки или <c>null</c>,
        /// если пользователь закрыл диалог без выбора либо выбор был отменён.
        /// </returns>
        Task<string?> PickFileAsync(string? initialDirectory, CancellationToken ct);
    }
}
