using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IMyMessageBox_ViewModel: IViewModel
    {
        /// <summary>
        /// фоновый цвет окна
        /// </summary>
        public Color BackColor { get; set; }
        /// <summary>
        /// цвет текста в окне
        /// </summary>
        public Color TextColor { get; set; }
        /// <summary>
        /// фоновый цвет левой кнопки
        /// </summary>
        public Color ButtonLeftBC { get; set; }
        /// <summary>
        /// цвет тескста левой кнопки
        /// </summary>
        public Color BullonLeftFC { get; set; }
        /// <summary>
        /// фоновый цвет правой кнопки
        /// </summary>
        public Color ButtonRightBC { get; set; }
        /// <summary>
        /// цвет текста правой кнопки
        /// </summary>
        public Color ButtonRightFC { get; set; }
        /// <summary>
        /// заголовок окна
        /// </summary>
        public string Header { get; set; }
        /// <summary>
        /// сообщение в окне
        /// </summary>
        public string Message { get; set; }
    }
}
