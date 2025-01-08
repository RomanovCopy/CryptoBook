using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

namespace CryptoBook.ViewModels
{
    public class ProgressViewModel: ViewModelBase, IProgressViewModel, ICloseable, IShowable
    {

        private readonly ProgressModel progressModel;

        public event EventHandler RequestClose;
        public event EventHandler RequestShow;

        public double WindowWidth { get => progressModel.WindowWidth; set => progressModel.WindowWidth = value; }
        public double WindowHeight { get => progressModel.WindowHeight; set => progressModel.WindowHeight = value; }
        public double WindowTop { get => progressModel.WindowTop; set => progressModel.WindowTop = value; }
        public double WindowLeft { get => progressModel.WindowLeft; set => progressModel.WindowLeft = value; }


        public CancellationToken CancellationToken => progressModel.CancellationToken;


        public string OperationName { get => progressModel.OperationName; set => progressModel.OperationName = value; }

        public double Progress { get => progressModel.Progress; set => progressModel.Progress = value; }

        public string StatusMessage { get => progressModel.StatusMessage; set => progressModel.StatusMessage = value; }

        public bool IsOperationRunning { get => progressModel.IsOperationRunning; set =>progressModel.IsOperationRunning= value; }




        public ProgressViewModel()
        {
            progressModel = new ProgressModel();
            progressModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }

        public ICommand Canceled => canceled ??= new RelayCommand(progressModel.Execute_Canceled, progressModel.CanExecute_Canceled);
        RelayCommand canceled;

        public ICommand Loaded => loaded ??= new RelayCommand(progressModel.Execute_Loaded, progressModel.CanExecute_Loaded);
        RelayCommand loaded;

        public ICommand Closed => closed ??= new RelayCommand(progressModel.Execute_Closed, progressModel.CanExecute_Closed);
        RelayCommand closed;

        public ICommand Closing => closing ??= new RelayCommand(progressModel.Execute_Closing, progressModel.CanExecute_Closing);
        RelayCommand closing;

        public ICommand Close => close ??= new RelayCommand(progressModel.Execute_Close, progressModel.CanExecute_Close);
        RelayCommand close;

    }
}
