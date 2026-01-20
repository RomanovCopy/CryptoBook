using CryptoBook.DTO;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IDriveManagerService:IDisposable
    {
        /// <summary>
        /// Коллекция доступных для записи дисков (включая USB). Только для чтения.
        /// </summary>
        ReadOnlyObservableCollection<IDriveItem> WritableDrives { get; }

        /// <summary>
        /// Событие: подключён новый подходящий диск
        /// </summary>
        event Action<IDriveItem> DriveConnected;

        /// <summary>
        /// Событие: отключён диск (передаётся буква, например "E:")
        /// </summary>
        event Action<string> DriveDisconnected;

        /// <summary>
        /// Запуск мониторинга подключения/отключения дисков
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Остановка мониторинга
        /// </summary>
        void StopMonitoring();
    }
}
