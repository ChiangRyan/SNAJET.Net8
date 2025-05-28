using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection; // For App.Host
using Microsoft.Extensions.Logging;
using MQTTnet; // For MqttApplicationMessageReceivedEventArgs
using MQTTnet.Client;
using SANJET.Core.Constants.Enums; // For Permission
using SANJET.Core.Interfaces;
using SANJET.Core.Services; // 【重要】為了能使用 MqttService 的 ApplicationMessageReceivedAsync 事件
using SANJET.UI.Views.Pages; // For HomePage
using SANJET.UI.Views.Windows; // For LoginWindow
using System.Collections.ObjectModel;
using System.Text; // For Encoding
using System.Text.Json; // For JsonSerializer
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls; // For Frame
// using System.Linq; // 如果需要 Linq 功能

namespace SANJET.Core.ViewModels
{
    // 輔助類別，用於反序列化來自 ESP32 的 JSON Payload
    public class Esp32LedStatusPayload
    {
        public string? Status { get; set; } // e.g., "success", "error"
        public string? Message { get; set; } // e.g., "LED 已開啟"
        public string? DeviceId { get; set; } // 確保有此欄位
        public string? LedState { get; set; } // ESP32 可以選擇性地回傳 "ON" 或 "OFF" 來讓 C# 確認狀態
    }

    public class Esp32OnlineStatusPayload
    {
        public string? Status { get; set; } // e.g., "online"
        public string? IP { get; set; }
        public string? DeviceId { get; set; }
    }

    public partial class MainViewModel : ObservableObject /*, IDisposable // 如果需要手動清理資源 */
    {
        private readonly IAuthenticationService _authService;
        private readonly IMqttService _mqttService;
        private readonly ILogger<MainViewModel> _logger;
        private Frame? _mainContentFrame;



        [ObservableProperty]
        private string? _currentUser;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private bool _isHomeSelected;

        [ObservableProperty]
        private bool _canViewHome;

        [ObservableProperty]
        private bool _canControlDevice;

        [ObservableProperty]
        private bool _canAll;

        // 新增：用於儲存所有被發現的 ESP32 設備狀態
        [ObservableProperty]
        private ObservableCollection<DeviceStatusViewModel> _esp32Devices;

        // 可選：用於 UI 綁定當前選中的設備 (如果 UI 設計需要)
        [ObservableProperty]
        private DeviceStatusViewModel? _selectedEsp32Device;

        public MainViewModel(IAuthenticationService authService, IMqttService mqttService, ILogger<MainViewModel> logger)
        {
            _authService = authService; //
            _mqttService = mqttService; //
            _logger = logger; //

            Esp32Devices = [];

            UpdateLoginState(); //
            IsHomeSelected = true; //
            _ = InitializeMqttRelatedTasksAsync(); //
        }

        private async Task InitializeMqttRelatedTasksAsync() // 原 InitializeAsync
        {
            try
            {
                // REMOVE: await _mqttService.ConnectAsync(); // 連線操作已由 MqttClientConnectionService 處理
                _logger.LogInformation("MainViewModel: Assuming MQTT client is being connected/is connected by MqttClientConnectionService.");

                if (_mqttService is MqttService concreteMqttService) //
                {
                    concreteMqttService.ApplicationMessageReceivedAsync += HandleEsp32MqttMessagesAsync; //
                }
                else
                {
                    _logger.LogWarning("MainViewModel: _mqttService 不是 MqttService 型別，無法訂閱訊息事件。"); //
                }

                // 訂閱通用的設備狀態主題和 LED 狀態主題
                await _mqttService.SubscribeAsync("devices/+/status");      // 所有設備的上線/離線狀態
                await _mqttService.SubscribeAsync("devices/+/led/status");  // 所有設備回傳的 LED 狀態
                                                                            // 如果需要，也訂閱 Modbus 回應的通用主題
                                                                            // await _mqttService.SubscribeAsync("devices/+/modbus/read/response");
                                                                            // await _mqttService.SubscribeAsync("devices/+/modbus/write/response");
                _logger.LogInformation("MainViewModel: Subscribed to MQTT topics for multiple devices.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel: MQTT related task initialization failed (subscriptions, etc.)."); //
                // 可以設定一個總體的 MQTT 連線錯誤狀態
            }
        }


        // 處理從 ESP32 收到的 MQTT 訊息
        private Task HandleEsp32MqttMessagesAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("MainViewModel 收到 MQTT: 主題='{Topic}', 内容='{Payload}'", topic, payloadJson);

            // 從主題中解析 DeviceId (例如 "devices/esp32-001/status" -> "esp32-001")
            string? deviceId = ParseDeviceIdFromTopic(topic);
            if (string.IsNullOrEmpty(deviceId))
            {
                _logger.LogWarning("無法從主題 {Topic} 中解析 DeviceId", topic);
                return Task.CompletedTask;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 查找或創建設備的 ViewModel
                    var deviceVm = Esp32Devices.FirstOrDefault(d => d.DeviceId == deviceId);
                    if (deviceVm == null)
                    {
                        deviceVm = new DeviceStatusViewModel(deviceId);
                        Esp32Devices.Add(deviceVm);
                    }
                    deviceVm.LastUpdated = DateTime.UtcNow;

                    if (topic.EndsWith("/status")) // 例如 devices/esp32-001/status
                    {
                        var statusPayload = JsonSerializer.Deserialize<Esp32OnlineStatusPayload>(payloadJson);
                        if (statusPayload != null && statusPayload.DeviceId == deviceId) // 再次確認 payload 中的 DeviceId
                        {
                            if (statusPayload.Status == "online")
                            {
                                deviceVm.ConnectionStatus = "在線";
                                deviceVm.IpAddress = statusPayload.IP;
                                _logger.LogInformation("設備 {DeviceId} 在線，IP: {IP}", deviceId, statusPayload.IP);
                            }
                            else if (statusPayload.Status == "offline")
                            {
                                deviceVm.ConnectionStatus = "離線";
                                deviceVm.IpAddress = null; // 離線時清除 IP
                                _logger.LogInformation("設備 {DeviceId} 離線 (LWT).", deviceId);
                            }
                            else
                            {
                                deviceVm.ConnectionStatus = statusPayload.Status ?? "狀態未知";
                            }
                        }
                    }
                    else if (topic.EndsWith("/led/status")) // 例如 devices/esp32-001/led/status
                    {
                        var ledStatus = JsonSerializer.Deserialize<Esp32LedStatusPayload>(payloadJson);
                        // 確保 payload 中的 DeviceId 與從 topic 解析的一致，或 ESP32 回應的 payload 中有 DeviceId
                        if (ledStatus != null && (ledStatus.DeviceId == deviceId || string.IsNullOrEmpty(ledStatus.DeviceId) /*兼容舊格式*/ ))
                        {
                            _logger.LogInformation("收到設備 {DeviceId} LED 狀態回饋: {Message}", deviceId, ledStatus.Message);
                            if (ledStatus.Status == "success")
                            {
                                if (!string.IsNullOrEmpty(ledStatus.LedState))
                                {
                                    deviceVm.IsLedOn = (ledStatus.LedState == "ON");
                                }
                                // 如果 ESP32 不回傳 LedState，則 IsLedOn 的更新依賴於發送命令時的預期
                            }
                            else
                            {
                                _logger.LogWarning("設備 {DeviceId} LED 操作失敗: {Message}", deviceId, ledStatus.Message);
                            }
                        }
                    }
                    // 在此處添加對 Modbus 回應等其他主題的處理邏輯
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "反序列化 MQTT payload 失敗. DeviceId: {DeviceId}, Topic: {Topic}, Payload: {Payload}", deviceId, topic, payloadJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "處理 MQTT 訊息時發生錯誤. DeviceId: {DeviceId}, Topic: {Topic}", deviceId, topic);
                }
            });
            return Task.CompletedTask;
        }
        private string? ParseDeviceIdFromTopic(string topic)
        {
            // 假設主題格式為 "devices/<DeviceId>/<endpoint>"
            var parts = topic.Split('/');
            if (parts.Length >= 2 && parts[0] == "devices")
            {
                return parts[1];
            }
            return null;
        }

        [RelayCommand]
        private async Task ToggleLed(DeviceStatusViewModel? device) // 命令現在需要指定設備
        {
            if (device == null || string.IsNullOrEmpty(device.DeviceId))
            {
                _logger.LogWarning("ToggleLed: 未選擇有效設備。");
                MessageBox.Show("請先選擇一個設備！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!CanControlDevice)
            {
                _logger.LogWarning("無權限控制設備 {DeviceId}，當前用戶: {CurrentUser}", device.DeviceId, CurrentUser);
                MessageBox.Show("您沒有權限控制設備！", "權限錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool desiredNewState = !device.IsLedOn;
            var commandPayload = new { state = desiredNewState ? "ON" : "OFF" }; // ESP32 期望的 payload
            string jsonPayload = JsonSerializer.Serialize(commandPayload);

            // 構建特定設備的命令主題
            string commandTopic = $"devices/{device.DeviceId}/led/set";

            try
            {
                await _mqttService.PublishAsync(commandTopic, jsonPayload);
                _logger.LogInformation("已發送 LED 控制指令到 {Topic}: {Payload}", commandTopic, jsonPayload);

                // 樂觀更新 UI (可以等待 ESP32 的 /led/status 回應來確認)
                // device.IsLedOn = desiredNewState; // 或者等待 HandleEsp32MqttMessagesAsync 中的更新
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT LED 控制訊息失敗到 {Topic}", commandTopic);
                MessageBox.Show($"無法控制設備 {device.DeviceId} 的 LED，請檢查 MQTT 連線。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        public void UpdateLoginState()
        {
            var currentUserObject = _authService.GetCurrentUser();
            CurrentUser = currentUserObject?.Username;
            IsLoggedIn = currentUserObject != null;

            if (IsLoggedIn && currentUserObject != null && currentUserObject.PermissionsList != null)
            {
                CanViewHome = currentUserObject.PermissionsList.Contains(Permission.ViewHome.ToString()) ||
                              currentUserObject.PermissionsList.Contains(Permission.All.ToString());
                CanControlDevice = currentUserObject.PermissionsList.Contains(Permission.ControlDevice.ToString()) ||
                                   currentUserObject.PermissionsList.Contains(Permission.All.ToString());
                CanAll = currentUserObject.PermissionsList.Contains(Permission.All.ToString());

            }
            else
            {
                CanViewHome = false;
                CanControlDevice = false;
                CanAll = false;
                _mainContentFrame?.Navigate(null); // 清空主內容區域
                Esp32Devices?.Clear(); // 清空設備列表
                _mainContentFrame?.Navigate(null);
            }

            // 登入成功且首頁應顯示時，導航到首頁
            if (IsLoggedIn && IsHomeSelected && _mainContentFrame != null)
            {
                // 檢查 MainContentFrame 是否已經是 HomePage，如果不是或為 null，則導航
                if (_mainContentFrame.Content is not HomePage)
                {
                    _ = NavigateHomeAsync(); // 導航到首頁
                }
            }
            else if (!IsLoggedIn && _mainContentFrame != null) // 如果未登入，確保 Frame 內容被清除
            {
                _mainContentFrame.Navigate(null);
            }
        }
        public void SetMainContentFrame(Frame frame)
        {
            _mainContentFrame = frame ?? throw new ArgumentNullException(nameof(frame));
            // 如果 ViewModel 已經是登入狀態且首頁被選中，但 Frame 是空的 (例如剛啟動 MainWindow)
            if (IsLoggedIn && IsHomeSelected && _mainContentFrame.Content == null)
            {
                _ = NavigateHomeAsync();
            }
        }
        [RelayCommand]
        private async Task NavigateHomeAsync()
        {
            if (_mainContentFrame != null)
            {
                IsHomeSelected = true; // 確保選中狀態
                // 檢查是否已經是 HomePage，避免不必要的重複導航
                if (_mainContentFrame.Content is not HomePage)
                {
                    var homePage = new HomePage();
                    if (App.Host != null)
                    {
                        var homeViewModel = App.Host.Services.GetService<HomeViewModel>();
                        if (homeViewModel != null)
                        {
                            // 傳遞必要的權限給 HomeViewModel
                            homeViewModel.CanControlDevice = CanControlDevice;
                            homePage.DataContext = homeViewModel;
                            await homeViewModel.LoadDevicesAsync(); // 載入設備數據
                        }
                    }
                    _mainContentFrame.Navigate(homePage);
                }
            }
        }
        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            UpdateLoginState(); // 這會處理 UI 更新和 Frame 清空
            // Esp32ConnectionStatus 已在 UpdateLoginState 中處理
        }
        [RelayCommand]
        private void ShowLogin()
        {
            if (App.Host != null)
            {
                var loginWindow = App.Host.Services.GetRequiredService<LoginWindow>();
                loginWindow.Owner = Application.Current.MainWindow; // 設定擁有者
                bool? result = loginWindow.ShowDialog();

                if (result == true) // 登入成功
                {
                    UpdateLoginState(); // 更新登入狀態並可能導航到首頁
                }
                // 如果登入失敗或取消，UpdateLoginState 已經在之前確保了 UI 是未登入狀態
            }
        }

        // 可選：如果 MainViewModel 需要清理 MQTT 訂閱 (例如，如果它不是應用程式生命週期單例)
        // public void Dispose()
        // {
        //     if (_mqttService is MqttService concreteMqttService)
        //     {
        //         concreteMqttService.ApplicationMessageReceivedAsync -= HandleEsp32MqttMessagesAsync;
        //     }
        //     // 可以考慮取消訂閱主題
        //     // _mqttService.UnsubscribeAsync("esp32/status").GetAwaiter().GetResult();
        //     // _mqttService.UnsubscribeAsync("esp32/led/status").GetAwaiter().GetResult();
        //     _logger.LogInformation("MainViewModel disposed and MQTT handlers cleaned up.");
        // }
    }
}