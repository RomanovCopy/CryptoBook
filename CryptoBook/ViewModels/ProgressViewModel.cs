using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CryptoBook.Infrastructure;
using CryptoBook.Models;

namespace CryptoBook.ViewModels
{
    public class ProgressViewModel: ViewModelBase
    {

        private readonly ProgressModel progressModel;

        public string OperationName { get => progressModel.OperationName; set => progressModel.OperationName = value; }

        public double Progress { get => progressModel.Progress; set => progressModel.Progress = value; }

        public string StatusMessage { get => progressModel.StatusMessage; set => progressModel.StatusMessage = value; }

        public bool IsOperationRunning { get => progressModel.IsOperationRunning; set =>progressModel.IsOperationRunning= value; }




        public ProgressViewModel()
        {
            progressModel = new ProgressModel();
            progressModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }





    }
}
