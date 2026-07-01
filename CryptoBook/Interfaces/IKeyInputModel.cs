using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IKeyInputModel :IModel, IWindowWithId, IWindowOptions
    {
        /// <summary>
        /// Заголовок окна ввода ключа (например, "Введите пароль").
        /// </summary>
        string Title{ get; }

        /// <summary>
        /// Описательное сообщение или подсказка для пользователя.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Указывает, необходимо ли показывать поле для повтора пароля (для подтверждения).
        /// Свойство инициализируемое (init) — задаётся при создании модели.
        /// </summary>
        bool ShowRepeatPassword { get; init; }
    }
}
