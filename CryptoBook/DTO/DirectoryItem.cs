using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class DirectoryItem: ContainerSystemItem, IDirectoryItem
    {
        private readonly IDispatcherService dispatcherService;
        private readonly IDirectoryMonitoringService directoryMonitoringService;

        public DirectoryItem(IDispatcherService dispatcherService,IDirectoryMonitoringService directoryMonitoringService):base(dispatcherService,directoryMonitoringService)
        {   
            this.dispatcherService = dispatcherService;
            this.directoryMonitoringService = directoryMonitoringService;
        }
    }
}
