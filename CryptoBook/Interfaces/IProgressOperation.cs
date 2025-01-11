using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IProgressOperation<T>
    {
        /// <summary>
        /// Выполняет операцию с поддержкой прогресса и отмены.
        /// </summary>
        /// <param name="progress">Объект для отслеживания прогресса.</param>
        /// <param name="cancellationToken">Токен для отмены операции.</param>
        /// <returns>Задача, представляющая выполнение операции.</returns>
        Task ExecuteAsync(IProgress<T> progress, CancellationToken cancellationToken);

        /// <summary>
        /// Восстанавливает или откатывает изменения после отмены операции.
        /// </summary>
        /// <returns>Задача, представляющая выполнение восстановления.</returns>
        Task RollbackAsync();
    }
}
