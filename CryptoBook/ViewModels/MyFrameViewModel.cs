using CryptoBook.Infrastructure;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.ViewModels
{
    public class MyFrameViewModel: ViewModelBase
    {
        private readonly MyFrameModel myFrameModel;


        public MyFrameViewModel()
        {
            myFrameModel = new ();
            myFrameModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


    }
}
