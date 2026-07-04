using KeyLogger.Interfaces;

using KeyLogger.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KeyLogger.ViewModels
{
    public class MainViewModel:ViewModelBase
    {
        private readonly IKeyboardHookService? _hookService;

        public MainViewModel(IKeyboardHookService hookService)
        {
            _hookService = hookService;
        }

        public ICommand? OnExit=>onExit ??= new RelayCommand(_ => System.Windows.Application.Current.Shutdown());
        RelayCommand? onExit;


    }
}
