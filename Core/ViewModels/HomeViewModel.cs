using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace SANJET.Core.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        public HomeViewModel()
        {
            // 初始化設備列表 - 這裡需要根據您的實際需求來實現
            Devices = new ObservableCollection<DeviceViewModel>();
            LoadDevices();
        }

        [ObservableProperty]
        private ObservableCollection<DeviceViewModel> devices = new();

        [ObservableProperty]
        private bool canControlDevice;

        private void LoadDevices()
        {
            // 這裡應該加載實際的設備數據
            // 暫時添加一些測試數據
            Devices.Add(new DeviceViewModel
            {
                Name = "設備1",
                IpAddress = "192.168.1.100",
                SlaveId = 1,
                Status = "閒置",
                IsOperational = true
            });
            // 添加更多設備...
        }
    }

    // 設備 ViewModel（如果不存在的話）
    public partial class DeviceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string ipAddress = string.Empty;

        [ObservableProperty]
        private int slaveId;

        [ObservableProperty]
        private string status = "閒置";

        [ObservableProperty]
        private bool isOperational = true;

        [ObservableProperty]
        private int runCount;

        [RelayCommand]
        private void Start()
        {
            // 啟動設備邏輯
            Status = "運行中";
        }

        [RelayCommand]
        private void Stop()
        {
            // 停止設備邏輯
            Status = "閒置";
        }

        [RelayCommand]
        private void Record()
        {
            // 記錄邏輯
        }
    }
}