// Path: Core/ViewModels/HomeViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using SANJET.UI.Views.Windows;

namespace SANJET.Core.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<HomeViewModel> _logger;
        private readonly MainViewModel? _mainViewModel;

        [ObservableProperty]
        private ObservableCollection<DeviceViewModel> devices = new();

        [ObservableProperty]
        private bool canControlDevice;

        public HomeViewModel(AppDbContext dbContext, ILogger<HomeViewModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mainViewModel = App.Host?.Services.GetService<MainViewModel>();
        }

        public async Task LoadDevicesAsync()
        {
            Devices.Clear();
            if (_dbContext.Devices == null)
            {
                _logger.LogWarning("HomeViewModel.LoadDevicesAsync: _dbContext.Devices is null.");
                return;
            }
            var devicesFromDb = await _dbContext.Devices.ToListAsync();
            foreach (var deviceEntity in devicesFromDb)
            {
                var deviceVm = new DeviceViewModel(this, _mainViewModel, _logger)
                {
                    Id = deviceEntity.Id,
                    Name = deviceEntity.Name,
                    OriginalName = deviceEntity.Name,
                    SlaveId = deviceEntity.SlaveId,
                    Status = deviceEntity.Status,
                    IsOperational = deviceEntity.IsOperational,
                    RunCount = deviceEntity.RunCount,
                    IsEditingName = false,
                    ControllingEsp32MqttId = deviceEntity.ControllingEsp32MqttId
                };

                if (string.IsNullOrEmpty(deviceVm.ControllingEsp32MqttId))
                {
                    _logger.LogWarning("資料庫中的設備 {DeviceName} (ID: {DeviceId}, SlaveID: {SlaveId}) 未設定 ControllingEsp32MqttId，將無法透過 MQTT 控制 Modbus。",
                                       deviceVm.Name, deviceVm.Id, deviceVm.SlaveId);
                }
                Devices.Add(deviceVm);
            }
        }

        public async Task SaveChangesToDeviceAsync(DeviceViewModel deviceVm)
        {
            if (deviceVm == null || _dbContext.Devices == null) return;

            var deviceInDb = await _dbContext.Devices.FindAsync(deviceVm.Id);
            if (deviceInDb != null)
            {
                deviceInDb.Name = deviceVm.Name;
                deviceInDb.IsOperational = deviceVm.IsOperational;
                _dbContext.Devices.Update(deviceInDb);
                await _dbContext.SaveChangesAsync();
                deviceVm.OriginalName = deviceVm.Name;
                _logger.LogInformation("已保存設備 ID {DeviceId} 的變更。", deviceVm.Id);
            }
        }

        // 重載 1: 由 MainViewModel 的 Modbus 讀取回應處理器呼叫 (輪詢更新)
        public void UpdateDeviceStatusFromMqtt(string esp32MqttIdFromResponse, byte slaveIdFromResponse, string newStatusTextFromDb, int newRunCountFromDb, string? contextMessage)
        {
            var deviceToUpdate = Devices.FirstOrDefault(d =>
                d.ControllingEsp32MqttId == esp32MqttIdFromResponse &&
                d.SlaveId == slaveIdFromResponse);

            if (deviceToUpdate != null)
            {
                string oldStatus = deviceToUpdate.Status;
                int oldRunCount = deviceToUpdate.RunCount;
                string finalStatus = newStatusTextFromDb;

                if (newStatusTextFromDb == "通訊失敗" && contextMessage != "資料已從 Modbus 更新" && !string.IsNullOrEmpty(contextMessage))
                {
                    finalStatus = $"{newStatusTextFromDb} ({contextMessage})";
                }

                bool statusChanged = deviceToUpdate.Status != finalStatus;
                bool runCountChanged = deviceToUpdate.RunCount != newRunCountFromDb;

                if (statusChanged)
                {
                    deviceToUpdate.Status = finalStatus;
                }
                if (runCountChanged)
                {
                    deviceToUpdate.RunCount = newRunCountFromDb;
                }

                if (statusChanged || runCountChanged)
                {
                    _logger.LogInformation("UI Update (Polling): Device '{DeviceName}' (ESP32: {Esp32Id}, Slave: {SlaveId}) updated. Status: '{OldStatus}' -> '{NewStatus}', RunCount: {OldRunCount} -> {NewRunCount}. Context: {Context}",
                                           deviceToUpdate.Name, esp32MqttIdFromResponse, slaveIdFromResponse, oldStatus, deviceToUpdate.Status, oldRunCount, deviceToUpdate.RunCount, contextMessage);
                }
            }
            else
            {
                _logger.LogWarning("UI Update (Polling): Device not found. ESP32: {Esp32Id}, Slave: {SlaveId}. Cannot update Status/RunCount from polling data.",
                                   esp32MqttIdFromResponse, slaveIdFromResponse);
            }
        }

        // 重載 2: 由 MainViewModel 的 Modbus 寫入回應處理器呼叫 (命令回饋)
        public void UpdateDeviceStatusFromMqtt(string esp32MqttIdFromResponse, byte slaveIdFromResponse, string responseStatus, string? responseMessage)
        {
            var deviceToUpdate = Devices.FirstOrDefault(d =>
                d.ControllingEsp32MqttId == esp32MqttIdFromResponse &&
                d.SlaveId == slaveIdFromResponse);

            if (deviceToUpdate != null)
            {
                string oldStatus = deviceToUpdate.Status;
                string newUiStatus = oldStatus;

                if (responseStatus.ToLower().Contains("success") || responseStatus.ToLower().Contains("成功"))
                {
                    if (oldStatus.Contains("啟動中"))
                    {
                        newUiStatus = "運行中";
                    }
                    else if (oldStatus.Contains("停止中"))
                    {
                        newUiStatus = "閒置";
                    }
                    else
                    {
                        newUiStatus = $"命令執行成功 ({responseMessage ?? "操作成功"})";
                    }
                }
                else
                {
                    newUiStatus = $"命令執行失敗 ({responseMessage ?? responseStatus})";
                }

                if (oldStatus != newUiStatus)
                {
                    deviceToUpdate.Status = newUiStatus;
                    _logger.LogInformation("UI Update (Command): Device '{DeviceName}' (ESP32: {Esp32Id}, Slave: {SlaveId}) Status updated: '{OldStatus}' -> '{NewStatus}'. Response: {ResponseStatus} - {ResponseMessage}",
                                           deviceToUpdate.Name, esp32MqttIdFromResponse, slaveIdFromResponse, oldStatus, newUiStatus, responseStatus, responseMessage);
                }
            }
            else
            {
                _logger.LogWarning("UI Update (Command): Device not found. ESP32: {Esp32Id}, Slave: {SlaveId}. Cannot update status from command response.",
                                   esp32MqttIdFromResponse, slaveIdFromResponse);
            }
        }
    }

    // DeviceViewModel 類別保持不變 (如先前提供)
    public partial class DeviceViewModel : ObservableObject
    {
        private readonly HomeViewModel? _homeViewModel;
        private readonly MainViewModel? _mainViewModel;
        private readonly ILogger? _logger;

        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string originalName = string.Empty;

        [ObservableProperty]
        private int slaveId;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private string status = "閒置";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool isOperational = true;

        [ObservableProperty]
        private int runCount;

        [ObservableProperty]
        private bool isEditingName = false;

        [ObservableProperty]
        private string? controllingEsp32MqttId;

        private const ushort MODBUS_CONTROL_REGISTER_ADDRESS = 0;
        private const ushort MODBUS_VALUE_START = 1;
        private const ushort MODBUS_VALUE_STOP = 0;

        public DeviceViewModel(HomeViewModel homeViewModel, MainViewModel? mainViewModel, ILogger? logger)
        {
            _homeViewModel = homeViewModel;
            _mainViewModel = mainViewModel;
            _logger = logger;
        }

        public DeviceViewModel()
        {
            _homeViewModel = App.Host?.Services.GetService<HomeViewModel>();
            _mainViewModel = App.Host?.Services.GetService<MainViewModel>();
            _logger = App.Host?.Services.GetService<ILogger<DeviceViewModel>>();
        }

        partial void OnIsOperationalChanged(bool value)
        {
            if (_homeViewModel != null)
            {
                _ = _homeViewModel.SaveChangesToDeviceAsync(this);
            }
        }

        [RelayCommand]
        private void EditName()
        {
            OriginalName = Name;
            IsEditingName = true;
        }

        [RelayCommand]
        private async Task SaveNameAsync()
        {
            IsEditingName = false;
            if (_homeViewModel != null)
            {
                await _homeViewModel.SaveChangesToDeviceAsync(this);
            }
        }

        [RelayCommand]
        private void CancelEditName()
        {
            Name = OriginalName;
            IsEditingName = false;
        }

        private bool CanStart()
        {
            return IsOperational &&
                   !string.IsNullOrEmpty(ControllingEsp32MqttId) &&
                   (Status == "閒置" || Status.Contains("失敗") || Status.Contains("通訊失敗") || Status.Contains("錯誤") || Status == "操作成功" || Status.Contains("命令成功") || Status.Contains("命令執行成功"));
        }

        private bool CanStop()
        {
            return IsOperational &&
                   !string.IsNullOrEmpty(ControllingEsp32MqttId) &&
                   (Status == "運行中" || Status.Contains("啟動中"));
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task StartAsync()
        {
            if (_mainViewModel == null || string.IsNullOrEmpty(ControllingEsp32MqttId))
            {
                MessageBox.Show("通訊服務或目標 ESP32 ID 未設定。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (SlaveId <= 0)
            {
                MessageBox.Show("無效的 Slave ID。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Status = "啟動中...";
            bool success = await _mainViewModel.SendModbusWriteCommandAsync(
                ControllingEsp32MqttId,
                (byte)SlaveId,
                MODBUS_CONTROL_REGISTER_ADDRESS,
                MODBUS_VALUE_START);

            if (success)
            {
                _logger?.LogInformation("已為 Slave ID {SlaveId} (由 ESP32 {Esp32Id}) 發送啟動命令。", SlaveId, ControllingEsp32MqttId);
            }
            else
            {
                Status = "啟動命令發送失敗";
            }
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task StopAsync()
        {
            if (_mainViewModel == null || string.IsNullOrEmpty(ControllingEsp32MqttId))
            {
                MessageBox.Show("通訊服務或目標 ESP32 ID 未設定。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (SlaveId <= 0)
            {
                MessageBox.Show("無效的 Slave ID。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Status = "停止中...";
            bool success = await _mainViewModel.SendModbusWriteCommandAsync(
                ControllingEsp32MqttId,
                (byte)SlaveId,
                MODBUS_CONTROL_REGISTER_ADDRESS,
                MODBUS_VALUE_STOP);

            if (success)
            {
                _logger?.LogInformation("已為 Slave ID {SlaveId} (由 ESP32 {Esp32Id}) 發送停止命令。", SlaveId, ControllingEsp32MqttId);
            }
            else
            {
                Status = "停止命令發送失敗";
            }
        }

        [RelayCommand]
        private void Record()
        {
            if (App.Host == null)
            {
                _logger?.LogError("無法開啟紀錄視窗，因為 App.Host 為 null。");
                return;
            }

            try
            {
                _logger?.LogInformation("為設備 {Name} (SlaveID: {SlaveId}) 開啟紀錄視窗。", Name, SlaveId);

                // **更正：建立一個獨立的 DI Scope 來管理服務的生命週期**
                using var scope = App.Host.Services.CreateScope();
                var provider = scope.ServiceProvider;

                // 從這個 scope 內解析服務
                var dbContext = provider.GetRequiredService<AppDbContext>();
                var recordLogger = provider.GetRequiredService<ILogger<RecordViewModel>>();
                var authService = provider.GetRequiredService<IAuthenticationService>();

                var currentUser = authService.GetCurrentUser()?.Username ?? "未知使用者";

                // 1. 建立 RecordViewModel
                var recordViewModel = new RecordViewModel(this, dbContext, recordLogger, currentUser);

                // 2. 建立 RecordWindow
                var recordWindow = new RecordWindow(recordViewModel)
                {
                    Owner = Application.Current.MainWindow,
                    Title = $"{this.Name} - 設備紀錄"
                };

                // 3. 顯示視窗 (在此期間，scope 內的服務會保持存活)
                recordWindow.ShowDialog();
                // 當視窗關閉後，using 區塊結束，scope 和裡面的服務 (如 dbContext) 會被自動釋放
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "開啟紀錄視窗時發生錯誤。");
                MessageBox.Show($"無法開啟紀錄視窗: {ex.Message}", "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}