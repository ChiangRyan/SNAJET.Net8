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
        // public string? LedState { get; set; } // ESP32 可以選擇性地回傳 "ON" 或 "OFF" 來讓 C# 確認狀態
    }

    public class Esp32OnlineStatusPayload
    {
        public string? Status { get; set; } // e.g., "online"
        public string? IP { get; set; }
    }

    public partial class MainViewModel : ObservableObject /*, IDisposable // 如果需要手動清理資源 */
    {
        private readonly IAuthenticationService _authService;
        private readonly IMqttService _mqttService;
        private readonly ILogger<MainViewModel> _logger;
        private Frame? _mainContentFrame;

        [ObservableProperty]
        private bool _isLedOn; // 這個狀態現在會嘗試與 ESP32 同步

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

        [ObservableProperty]
        private string _esp32ConnectionStatus = "ESP32: 未知"; // 用於在 UI 顯示 ESP32 連線狀態

        public MainViewModel(IAuthenticationService authService, IMqttService mqttService, ILogger<MainViewModel> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService)); // 正確注入
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            UpdateLoginState();
            IsHomeSelected = true; // 預設選中首頁
            _ = InitializeMqttRelatedTasksAsync(); // 初始化 MQTT 相關操作
        }

        private async Task InitializeMqttRelatedTasksAsync() // 原 InitializeAsync
        {
            try
            {
                await _mqttService.ConnectAsync(); // 連接 C# 的 MQTT Client 到 C# 的 Broker
                _logger.LogInformation("MainViewModel: MQTT 客戶端連接成功。");

                // 訂閱來自 ESP32 的狀態主題
                // 需要將 _mqttService 轉型為 MqttService 才能訪問 ApplicationMessageReceivedAsync 事件
                if (_mqttService is MqttService concreteMqttService)
                {
                    concreteMqttService.ApplicationMessageReceivedAsync += HandleEsp32MqttMessagesAsync;
                }
                else
                {
                    _logger.LogWarning("MainViewModel: _mqttService 不是 MqttService 型別，無法訂閱訊息事件。");
                }

                // 訂閱我們關心的主題
                await _mqttService.SubscribeAsync("esp32/status");      // ESP32 上線狀態
                await _mqttService.SubscribeAsync("esp32/led/status");  // ESP32 回傳的 LED 狀態
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel: MQTT 初始化失敗。");
                // 可以在 UI 上顯示錯誤提示
                Esp32ConnectionStatus = "ESP32: MQTT 連線失敗";
            }
        }

        // 處理從 ESP32 收到的 MQTT 訊息
        private Task HandleEsp32MqttMessagesAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("MainViewModel 收到 ESP32 MQTT: 主題='{Topic}', 内容='{Payload}'", topic, payloadJson);

            Application.Current.Dispatcher.Invoke(() => // 確保 UI 更新在主執行緒
            {
                try
                {
                    if (topic == "esp32/status")
                    {
                        var statusPayload = JsonSerializer.Deserialize<Esp32OnlineStatusPayload>(payloadJson);
                        if (statusPayload?.Status == "online")
                        {
                            Esp32ConnectionStatus = $"ESP32: 在線 ({statusPayload.IP})";
                            _logger.LogInformation("ESP32 在線，IP: {IP}", statusPayload.IP);
                            // 可以考慮在 ESP32 上線後，主動查詢一次 LED 的當前狀態
                            // await QueryLedStatusAsync(); // 如果有這個功能
                        }
                        else
                        {
                            Esp32ConnectionStatus = $"ESP32: {statusPayload?.Status ?? "狀態未知"}";
                        }
                    }
                    else if (topic == "esp32/led/status")
                    {
                        var ledStatus = JsonSerializer.Deserialize<Esp32LedStatusPayload>(payloadJson);
                        if (ledStatus != null)
                        {
                            _logger.LogInformation("收到 ESP32 LED 狀態回饋: {Message}", ledStatus.Message);
                            if (ledStatus.Status == "success")
                            {
                                // ESP32 的 Arduino 程式碼中，handleMqttSetLED 並沒有直接回傳 LED 是 ON 還是 OFF
                                // 它只回傳操作是否成功。所以這裡 IsLedOn 的更新依賴於 ToggleLed 中的預期狀態。
                                // 如果 ESP32 回傳了實際狀態 (例如新增一個 LedState 欄位)，則可以這樣更新：
                                // if (!string.IsNullOrEmpty(ledStatus.LedState)) IsLedOn = (ledStatus.LedState == "ON");
                                // 目前，我們依賴 ToggleLed 中的 UI 變更。
                            }
                            else
                            {
                                _logger.LogWarning("ESP32 LED 操作失敗: {Message}", ledStatus.Message);
                                // 可以考慮彈出提示或恢復 IsLedOn 到操作前的狀態
                            }
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "反序列化 ESP32 MQTT payload 失敗. Topic: {Topic}, Payload: {Payload}", topic, payloadJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "處理 ESP32 MQTT 訊息時發生錯誤. Topic: {Topic}", topic);
                }
            });
            return Task.CompletedTask;
        }


        [RelayCommand]
        private async Task ToggleLed()
        {
            if (!CanControlDevice)
            {
                _logger.LogWarning("無權限控制設備，當前用戶: {CurrentUser}", CurrentUser);
                MessageBox.Show("您沒有權限控制設備！", "權限錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. 決定期望的 LED 新狀態
            bool desiredNewState = !IsLedOn;

            // 2. 準備要發送到 ESP32 的 JSON Payload
            var commandPayload = new { state = desiredNewState ? "ON" : "OFF" };
            string jsonPayload = JsonSerializer.Serialize(commandPayload);

            // 3. 透過 MQTT 發送指令
            try
            {
                await _mqttService.PublishAsync("esp32/led/set", jsonPayload); // 主題改為 esp32/led/set
                _logger.LogInformation("已發送 LED 控制指令到 esp32/led/set: {Payload}", jsonPayload);

                // 4. 樂觀更新 UI 上的 LED 狀態
                //    實際狀態的確認依賴 HandleEsp32MqttMessagesAsync 中對 esp32/led/status 的處理
                //    如果 ESP32 回傳的 status payload 包含實際的 ON/OFF 狀態，那裡的邏輯會更精確。
                //    目前 ESP32 的 handleMqttSetLED 只回傳操作成功與否，所以這裡的UI更新是基於指令已發送。
                IsLedOn = desiredNewState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT LED 控制訊息失敗");
                MessageBox.Show("無法控制 LED，請檢查 MQTT 連線。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                // 如果發送失敗，可以考慮將 IsLedOn 恢復到 !desiredNewState
            }
        }

        // --- 其餘 MainViewModel 的方法 (UpdateLoginState, SetMainContentFrame, NavigateHomeAsync, Logout, ShowLogin) 保持不變 ---
        // 確保在這些方法中，ESP32ConnectionStatus 在登出或未登入時被適當重置。
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
                Esp32ConnectionStatus = "ESP32: 未知 (請先登入)"; // 重置狀態
                _mainContentFrame?.Navigate(null); // 清空主內容區域
            }

            // 登入成功且首頁應顯示時，導航到首頁
            if (IsLoggedIn && IsHomeSelected && _mainContentFrame != null)
            {
                // 檢查 MainContentFrame 是否已經是 HomePage，如果不是或為 null，則導航
                if (!(_mainContentFrame.Content is HomePage))
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
                if (!(_mainContentFrame.Content is HomePage))
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