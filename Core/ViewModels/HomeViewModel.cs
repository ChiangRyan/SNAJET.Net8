
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces; // 假設 MainViewModel 會透過此方式或 DI 取得
using System; // For DateTime
using System.Collections.ObjectModel;
using System.Linq; // 確保此 using 存在
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows; // For MessageBox
using SANJET.UI.Views.Windows; // <<-- 新增此 using 指示詞

namespace SANJET.Core.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<HomeViewModel> _logger;
        private readonly MainViewModel? _mainViewModel; // 用於讓 DeviceViewModel 可以呼叫 MainViewModel 的方法

        [ObservableProperty]
        private ObservableCollection<DeviceViewModel> devices = new(); //

        [ObservableProperty]
        private bool canControlDevice; //

        public HomeViewModel(AppDbContext dbContext, ILogger<HomeViewModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _mainViewModel = App.Host?.Services.GetService<MainViewModel>(); // 獲取 MainViewModel 實例
        }

        public async Task LoadDevicesAsync()
        {
            Devices.Clear(); //
            if (_dbContext.Devices == null)
            {
                _logger.LogWarning("HomeViewModel.LoadDevicesAsync: _dbContext.Devices is null.");
                return;
            }
            var devicesFromDb = await _dbContext.Devices.ToListAsync(); //
            foreach (var deviceEntity in devicesFromDb)
            {
                var deviceVm = new DeviceViewModel(this, _mainViewModel, _logger)
                {
                    Id = deviceEntity.Id, //
                    Name = deviceEntity.Name, //
                    OriginalName = deviceEntity.Name, //
                    // IpAddress = deviceEntity.IpAddress, // 不再映射 IP Address
                    SlaveId = deviceEntity.SlaveId, //
                    Status = deviceEntity.Status, //
                    IsOperational = deviceEntity.IsOperational, //
                    RunCount = deviceEntity.RunCount, //
                    IsEditingName = false, //
                    ControllingEsp32MqttId = deviceEntity.ControllingEsp32MqttId // 從資料庫實體獲取
                };

                if (string.IsNullOrEmpty(deviceVm.ControllingEsp32MqttId))
                {
                    _logger.LogWarning("資料庫中的設備 {DeviceName} (ID: {DeviceId}, SlaveID: {SlaveId}) 未設定 ControllingEsp32MqttId，將無法透過 MQTT 控制 Modbus。",
                                       deviceVm.Name, deviceVm.Id, deviceVm.SlaveId);
                }
                Devices.Add(deviceVm); //
            }
        }

        public async Task SaveChangesToDeviceAsync(DeviceViewModel deviceVm) //
        {
            if (deviceVm == null || _dbContext.Devices == null) return;

            var deviceInDb = await _dbContext.Devices.FindAsync(deviceVm.Id);
            if (deviceInDb != null)
            {
                deviceInDb.Name = deviceVm.Name;
                deviceInDb.IsOperational = deviceVm.IsOperational;
                // 如果 ControllingEsp32MqttId 也可以在 UI 修改並保存，則加入以下
                // deviceInDb.ControllingEsp32MqttId = deviceVm.ControllingEsp32MqttId;
                // deviceInDb.SlaveId = deviceVm.SlaveId; // 如果 SlaveId 也可以修改

                _dbContext.Devices.Update(deviceInDb);
                await _dbContext.SaveChangesAsync();
                deviceVm.OriginalName = deviceVm.Name; //
                _logger.LogInformation("已保存設備 ID {DeviceId} 的變更。", deviceVm.Id);
            }
        }

        public void UpdateDeviceStatusFromMqtt(string esp32MqttIdFromResponse, byte slaveIdFromResponse, string newStatusText, int newRunCount, string? messageFromResponse)
        {
            var deviceToUpdate = Devices.FirstOrDefault(d =>
                d.ControllingEsp32MqttId == esp32MqttIdFromResponse &&
                d.SlaveId == slaveIdFromResponse);

            if (deviceToUpdate != null)
            {
                string combinedMessage = string.IsNullOrEmpty(messageFromResponse) ? newStatusText : $"{newStatusText}: {messageFromResponse}";
                if (newStatusText.ToLower().Contains("success") || newStatusText.ToLower().Contains("成功"))
                {
                    if (deviceToUpdate.Status.Contains("啟動中")) deviceToUpdate.Status = "運行中";
                    else if (deviceToUpdate.Status.Contains("停止中")) deviceToUpdate.Status = "閒置";
                    else deviceToUpdate.Status = $"操作成功 ({messageFromResponse ?? ""})";
                }
                else
                {
                    deviceToUpdate.Status = $"操作失敗 ({messageFromResponse ?? newStatusText})";
                }
                _logger.LogInformation("從 MQTT 更新設備 (ESP32: {Esp32Id}, SlaveID: {SlaveId}) 狀態為: {Status}",
                                       esp32MqttIdFromResponse, slaveIdFromResponse, deviceToUpdate.Status);
            }
            else
            {
                _logger.LogWarning("收到來自 ESP32 {Esp32Id} 的 Modbus Slave {SlaveId} 的狀態更新，但在列表中找不到對應項。",
                                   esp32MqttIdFromResponse, slaveIdFromResponse);
            }
        }
    }

    public partial class DeviceViewModel : ObservableObject
    {
        private readonly HomeViewModel? _homeViewModel;
        private readonly MainViewModel? _mainViewModel;
        private readonly ILogger? _logger;

        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name = string.Empty; //

        [ObservableProperty]
        private string originalName = string.Empty; //

        // [ObservableProperty] // 已移除或註解
        // private string ipAddress = string.Empty;

        [ObservableProperty]
        private int slaveId; //

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))] // 當 Status 改變時，通知 StartCommand 的 CanExecute 狀態可能需要重新評估
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]  // 也通知 StopCommand
        private string status = "閒置"; //

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool isOperational = true; //

        [ObservableProperty]
        private int runCount; //

        [ObservableProperty]
        private bool isEditingName = false; //

        [ObservableProperty]
        private string? controllingEsp32MqttId;

        // Modbus 命令參數
        private const ushort MODBUS_CONTROL_REGISTER_ADDRESS = 0; // ESP32 端會加上 Address_Offset
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
            _homeViewModel = App.Host?.Services.GetService<HomeViewModel>(); //
            _mainViewModel = App.Host?.Services.GetService<MainViewModel>();
            _logger = App.Host?.Services.GetService<ILogger<DeviceViewModel>>();
        }

        partial void OnIsOperationalChanged(bool value) //
        {
            if (_homeViewModel != null)
            {
                _ = _homeViewModel.SaveChangesToDeviceAsync(this);
            }
        }

        [RelayCommand]
        private void EditName() //
        {
            OriginalName = Name; //
            IsEditingName = true; //
        }

        [RelayCommand]
        private async Task SaveNameAsync() //
        {
            IsEditingName = false; //
            if (_homeViewModel != null)
            {
                await _homeViewModel.SaveChangesToDeviceAsync(this); //
            }
        }

        [RelayCommand]
        private void CancelEditName() //
        {
            Name = OriginalName; //
            IsEditingName = false; //
        }

        private bool CanStart()
        {
            // 確保 ControllingEsp32MqttId 非空，並且符合 XAML 中 StartButtonStyle 的啟用邏輯
            return IsOperational &&
                   !string.IsNullOrEmpty(ControllingEsp32MqttId) &&
                   (Status == "閒置" || Status.Contains("失敗") || Status.Contains("通訊失敗") || Status.Contains("錯誤") || Status == "操作成功" || Status == "命令執行成功");
        }

        private bool CanStop()
        {
            // 確保 ControllingEsp32MqttId 非空，並且符合 XAML 中 StopButtonStyle 的啟用邏輯
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

        // RecordCommand 保持原樣或根據需要修改
        [RelayCommand]
        private void Record() //
        {
            // 記錄邏輯
            _logger?.LogInformation("設備 {Name} (SlaveID: {SlaveId}) 觸發紀錄。", Name, SlaveId);
            // 實際實現可能需要彈出一個新視窗或導航到記錄頁面，並傳遞此 DeviceViewModel 的資訊
            // 例如，可以透過 _mainViewModel 或一個導航服務來處理
            if (App.Host?.Services.GetService<RecordView>() is RecordView recordView)
            {
                // recordView.DataContext = new RecordViewModel(this); // 假設您有一個 RecordViewModel
                recordView.Owner = Application.Current.MainWindow;
                recordView.ShowDialog();
            }
        }
    }
}