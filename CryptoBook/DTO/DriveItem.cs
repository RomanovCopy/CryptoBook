using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class DriveItem: ContainerSystemItem, IDriveItem
    {
        private readonly IDispatcherService dispatcherService;    
        private readonly IDirectoryMonitoringService directoryMonitoringService;

        public string VolumeLabel { get => _volumeLabel; set => SetProperty(ref _volumeLabel, value); }
        string _volumeLabel;
        public string DriveFormat { get => _driveFormat; set => SetProperty(ref _driveFormat, value); }
        string _driveFormat;
        public DriveType DriveType { get => _driveType; set => SetProperty(ref _driveType,value); }
        DriveType _driveType;
        public long AvailableFreeSpace { get => _availableFreeSpace; set => SetProperty(ref _availableFreeSpace, value); }
        long _availableFreeSpace;
        public long TotalSize { get => _totalSize; set => SetProperty(ref _totalSize, value); }
        long _totalSize;

        public DriveItem(IDispatcherService dispatcherService, IDirectoryMonitoringService directoryMonitoringService) : base(dispatcherService,directoryMonitoringService)
        {
            this.dispatcherService = dispatcherService;
            this.directoryMonitoringService = directoryMonitoringService;
        }

    }
}
