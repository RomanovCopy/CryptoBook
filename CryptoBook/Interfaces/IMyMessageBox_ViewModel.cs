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
        public Brush BackColor { get; set; }
        public Brush TextColor { get; set; }
        public Brush ButtonLeftBC { get; set; }
        public Brush BullonLeftFC { get; set; }
        public Brush ButtonRightBC { get; set; }
        public Brush ButtonRightFC { get; set; }
        public string Header { get; set; }
        public string Message { get; set; }


        public ICommand SetHeader { get; }

        public ICommand SetMessage { get; }

    }
}
