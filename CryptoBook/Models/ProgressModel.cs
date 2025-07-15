using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

namespace CryptoBook.Models
{
    internal class ProgressModel: ViewModelBase
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;
        private readonly ILifetimeScope scope;
        internal readonly Guid WindowId;





        internal double WindowWidth { get => windowWidth; set => SetProperty(ref windowWidth, value); }
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




        public ProgressModel(ILifetimeScope scope)
        {
            Initialization();
            WindowId = Guid.NewGuid();
            this.scope = scope;
            cancellationTokenSource = new();
            cancellationToken = cancellationTokenSource.Token;
        }

        private void Initialization()
        {
            WindowHeight = Properties.Settings.Default.ProgressWindowHeight;
            WindowWidth = Properties.Settings.Default.ProgressWindowWidth;
            WindowTop = Properties.Settings.Default.ProgressWindowTop;
            WindowLeft = Properties.Settings.Default.ProgressWindowLeft;
        }


        internal bool CanExecute_StartLongOperation(object? obj)
        {
            return obj is IProgressOperation<double>;
        }
        internal async void Execute_StartLongOperation(object? obj)
        {
            if(obj is IProgressOperation<double> operation)
            {
                var progress = new Progress<double>(value => Progress = value);
                await operation.ExecuteAsync(progress, cancellationToken);
                scope.Resolve<IWindowManager>().CloseWindow<ProgressWindow>(WindowId);
            }

        }

        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
        }


        internal bool CanExecute_Canceled(object? obj)
        {
            IsOperationRunning = !cancellationToken.IsCancellationRequested;
            return IsOperationRunning;
        }
        internal void Execute_Canceled(object? obj)
        {
            cancellationTokenSource.Cancel();
            Execute_Close(null);
        }


        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
        {
            Properties.Settings.Default.ProgressWindowHeight = WindowHeight;
            Properties.Settings.Default.ProgressWindowWidth = WindowWidth;
            Properties.Settings.Default.ProgressWindowLeft = WindowLeft;
            Properties.Settings.Default.ProgressWindowTop = WindowTop;
            Properties.Settings.Default.Save();
        }


        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        internal void Execute_Closed(object? obj)
        {

        }


        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
            scope.Resolve<IWindowManager>().CloseWindow<ProgressWindow>(WindowId);
        }




    }
}
