using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MyFrameViewModel: ViewModelBase, IMyFrameViewModel
    {

        private readonly MyFrameModel myFrameModel;


        public MyFrameViewModel()
        {
            myFrameModel = new ();
            myFrameModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        public ICommand Loaded { get; }

        public ICommand Close { get; }

        public ICommand Closing { get; }
    }
}
