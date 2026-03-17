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
        private readonly ISystemItemCreateService systemItemCreateService;
        private readonly ISystemItemSortService systemItemSortService;

        public DirectoryItem( IDispatcherService dispatcherService,IDirectoryMonitoringService directoryMonitoringService,ISystemItemCreateService systemItemCreateService,ISystemItemSortService systemItemSortService 
        ):base( dispatcherService,directoryMonitoringService,systemItemCreateService,systemItemSortService)
        {   
            this.dispatcherService = dispatcherService;
            this.directoryMonitoringService = directoryMonitoringService;
            this.systemItemCreateService = systemItemCreateService;
            this.systemItemSortService = systemItemSortService;
        }
    }
}
