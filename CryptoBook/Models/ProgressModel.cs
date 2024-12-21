using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CryptoBook.Infrastructure;

namespace CryptoBook.Models
{
    internal class ProgressModel: ViewModelBase
    {
        private CancellationTokenSource cancellationTokenSource { get; set; }

        internal string OperationName { get => operationName; set => SetProperty(ref operationName, value); }
        private string operationName;

        internal double Progress { get => progress; set => SetProperty(ref progress, value); }
        private double progress;

        internal string StatusMessage { get => statusMessage; set => SetProperty(ref statusMessage, value); }
        private string statusMessage;

        internal bool IsOperationRunning { get => isOperationRunning; set => SetProperty(ref isOperationRunning, value); }
        private bool isOperationRunning;

        
        public ProgressModel()
        {

        }






    }
}
