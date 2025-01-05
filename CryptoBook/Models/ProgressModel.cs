using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

namespace CryptoBook.Models
{
    internal class ProgressModel: ViewModelBase
    {
        private CancellationTokenSource cancellationTokenSource { get; set; }

        internal CancellationToken CancellationToken { get => cancellationToken; private set => cancellationToken = value; }
        CancellationToken cancellationToken;

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




        public ProgressModel()
        {
            Initialization();
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
            IsOperationRunning = !CancellationToken.IsCancellationRequested;
            return IsOperationRunning;
        }
        internal void Execute_Canceled(object obj)
        {
            if(obj != null)
            {
                cancellationTokenSource.Cancel();
                Execute_Close(obj);
            }
        }


        internal bool CanExecute_Closing(object obj)
        {
            return true;
        }
        internal void Execute_Closing(object obj)
        {
            Properties.Settings.Default.ProgressWindowHeight = WindowHeight;
            Properties.Settings.Default.ProgressWindowWidth = WindowWidth;
            Properties.Settings.Default.ProgressWindowLeft = WindowLeft;
            Properties.Settings.Default.ProgressWindowTop = WindowTop;
        }


        internal bool CanExecute_Closed(object obj)
        {
            return true;
        }
        internal void Execute_Closed(object obj)
        {

        }


        internal bool CanExecute_Close(object? obj)
        {
            return true;
        }
        internal void Execute_Close(object? obj)
        {
            if(obj is Window window)
            {
                var winManager = App.Container.Resolve<IWindowManager>();
                winManager.CloseWindow(window);
            }
        }



        private async void Initialization()
        {
            cancellationTokenSource = new();
            cancellationToken = cancellationTokenSource.Token;
            WindowHeight = Properties.Settings.Default.ProgressWindowHeight;
            WindowWidth = Properties.Settings.Default.WindowWidth;
            WindowTop = Properties.Settings.Default.WindowTop;
            WindowLeft = Properties.Settings.Default.WindowLeft;
            await StartLongOperationAsync();
        }



        public async Task StartLongOperationAsync()
        {
            IsOperationRunning = true;
            StatusMessage = "Начало работы...";

            var progress = new Progress<int>(value =>
            {
                Progress = value;
                StatusMessage = $"Прогресс: {value}%";
            });

            await Task.Run(() => LongRunningOperation(progress));

            StatusMessage = "Операция завершена!";
            IsOperationRunning = false;
            
        }

        private void LongRunningOperation(IProgress<int> progress)
        {
            for(int i = 0; i <= 100; i++)
            {
                Thread.Sleep(50); // Эмуляция длительной операции
                progress.Report(i);
            }
        }

    }
}
