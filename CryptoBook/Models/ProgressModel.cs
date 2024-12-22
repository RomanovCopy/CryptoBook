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

        internal CancellationToken CancellationToken { get=>cancellationToken; private set=>cancellationToken=value; }
        CancellationToken cancellationToken;

        internal double WindowWidth { get=>windowWidth; set=>SetProperty(ref windowWidth, value); }
        double windowWidth;
        internal double WindowHeight { get => windowHeight; set => SetProperty(ref windowHeight, value); }
        double windowHeight;
        internal double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        internal double WindowLeft { get => windowLeft; set => SetProperty(ref windowLeft, value); }
        double windowLeft;



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
            cancellationTokenSource = new();
            cancellationToken = cancellationTokenSource.Token;
        }


        internal bool CanExecute_Loaded(object obj)
        {
            return true;
        }
        internal void Execute_Loaded(object obj)
        {
        }


        internal bool CanExecute_Canceled(object obj)
        {
            return !CancellationToken.IsCancellationRequested;
        }
        internal void Execute_Canceled(object obj)
        {
            cancellationTokenSource.Cancel();
        }


        internal bool CanExecute_Closing(object obj)
        {
            return true;
        }
        internal void Execute_Closing(object obj)
        {
        }


        internal bool CanExecute_Closed(object obj)
        {
            return true;
        }
        internal void Execute_Closed(object obj)
        {
        }

    }
}
