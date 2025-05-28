
using CommunityToolkit.Mvvm.ComponentModel;
using SANJET.Core.Models;
using System.Data;

namespace SANJET.Core.ViewModels
{
    public partial class DeviceStatusViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? deviceId;

        [ObservableProperty]
        private string? connectionStatus; // "在線", "離線", "未知"

        [ObservableProperty]
        private string? ipAddress;

        [ObservableProperty]
        private bool isLedOn; // 代表該設備的 LED 狀態

        // 可以添加最後更新時間等其他資訊
        [ObservableProperty]
        private DateTime lastUpdated;

        public DeviceStatusViewModel(string deviceId)
        {
            DeviceId = deviceId;
            ConnectionStatus = "未知";
            LastUpdated = DateTime.UtcNow;
        }
    }
}