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
        private readonly IFileManagerService fileManagerService;

        public DirectoryItem(IFileManagerService fileManagerService, IDispatcherService dispatcherService,IDirectoryMonitoringService directoryMonitoringService,ISystemItemCreateService systemItemCreateService):base(fileManagerService, dispatcherService,directoryMonitoringService,systemItemCreateService)
        {   
            this.dispatcherService = dispatcherService;
            this.directoryMonitoringService = directoryMonitoringService;
            this.systemItemCreateService = systemItemCreateService;
            this.fileManagerService = fileManagerService;
        }
    }
}
