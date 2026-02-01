using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IWindowOptions
    {
        double WindowWidth { get; set; }
        double WindowHeight { get; set; }
        public double WindowTop { get; set; }
        public double WindowLeft { get; set; }
        public WindowState WindowState { get; set; }
    }
}
