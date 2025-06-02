using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Client; // Potentially for MqttClientOptions, etc. if used directly, though likely through IMqttService
using SANJET.Core.Constants.Enums; // For Permission enum
using SANJET.Core.Interfaces;
using SANJET.Core.Services; // For MqttService concrete type check
using SANJET.UI.Views.Pages; // For HomePage
using SANJET.UI.Views.Windows; // For LoginWindow
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls; // For Frame


namespace SANJET.Core.ViewModels
{
    public class Esp32LedStatusPayload
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? DeviceId { get; set; }
        public string? LedState { get; set; }
    }

    public class Esp32OnlineStatusPayload
    {
        public string? Status { get; set; }
        public string? IP { get; set; }
        public string? DeviceId { get; set; }
    }

    public class ModbusWriteResponsePayload
    {
        public string? DeviceId { get; set; }
        public byte SlaveId { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
    }
    public class ModbusReadResponsePayload
    {
        public string? DeviceId { get; set; }
        public byte SlaveId { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
        public ushort[]? Data { get; set; }
        public byte FunctionCode { get; set; }
        public ushort Address { get; set; }
        public ushort Quantity { get; set; }
    }

    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IAuthenticationService _authService;
        private readonly IMqttService _mqttService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPollingStateService _pollingStateService; // 新增

        private Frame? _mainContentFrame;
        private bool _isDisposed = false;

        [ObservableProperty]
        private string? _currentUser; //

        [ObservableProperty]
        private bool _isLoggedIn; //

        [ObservableProperty]
        private bool _isHomeSelected; //

        [ObservableProperty]
        private bool _canViewHome; //

        [ObservableProperty]
        private bool _isSettingsSelected; // 新增或確認此屬性存在

        [ObservableProperty]
        private bool _canViewSettings; // 新增或確認此屬性存在，用於控制按鈕可見性

        [ObservableProperty]
        private bool _canControlDevice; //

        [ObservableProperty]
        private bool _canAll; //

        [ObservableProperty]
        private ObservableCollection<DeviceStatusViewModel> esp32Devices; //

        [ObservableProperty]
        private DeviceStatusViewModel? selectedEsp32Device; //

        public MainViewModel(
        IAuthenticationService authService,
        IMqttService mqttService,
        ILogger<MainViewModel> logger,
        IServiceProvider serviceProvider, // 
        IPollingStateService pollingStateService) // 
        {
            _authService = authService; //
            _mqttService = mqttService; //
            _logger = logger; //
            _serviceProvider = serviceProvider; //
            _pollingStateService = pollingStateService;

            this.esp32Devices = []; //

            UpdateLoginState(); //
            IsHomeSelected = true; //
            _ = InitializeMqttRelatedTasksAsync(); //
        }

        private async Task InitializeMqttRelatedTasksAsync() //
        {
            try
            {
                _logger.LogInformation("MainViewModel: Assuming MQTT client is being connected/is connected by MqttClientConnectionService."); //

                if (_mqttService is MqttService concreteMqttService) //
                {
                    concreteMqttService.ApplicationMessageReceivedAsync += HandleEsp32MqttMessagesAsync; //
                }
                else
                {
                    _logger.LogWarning("MainViewModel: _mqttService 不是 MqttService 型別，無法訂閱訊息事件。"); //
                }

                await _mqttService.SubscribeAsync("devices/+/status"); //
                await _mqttService.SubscribeAsync("devices/+/led/status"); //
                await _mqttService.SubscribeAsync("devices/+/modbus/write/response"); //
                await _mqttService.SubscribeAsync("devices/+/modbus/read/response"); //

                _logger.LogInformation("MainViewModel: Subscribed to MQTT topics for multiple devices including Modbus responses."); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel: MQTT related task initialization failed (subscriptions, etc.)."); //
            }
        }

        private Task HandleEsp32MqttMessagesAsync(MqttApplicationMessageReceivedEventArgs e) //
        {
            var topic = e.ApplicationMessage.Topic; //
            var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment); //
            _logger.LogDebug("MainViewModel 收到 MQTT: 主題='{Topic}', 内容='{Payload}'", topic, payloadJson); //

            string? parsedEsp32Id = ParseDeviceIdFromTopic(topic); //

            Application.Current.Dispatcher.Invoke(async () => //
            {
                if (_isDisposed)
                {
                    _logger.LogWarning("MainViewModel is disposed, skipping MQTT message handling for topic {Topic}.", topic);
                    return;
                }

                try
                {
                    if (topic.EndsWith("/status")) //
                    {
                        if (string.IsNullOrEmpty(parsedEsp32Id)) return; //

                        var statusPayload = JsonSerializer.Deserialize<Esp32OnlineStatusPayload>(payloadJson); //
                        if (statusPayload != null && statusPayload.DeviceId == parsedEsp32Id) //
                        {
                            var deviceVm = Esp32Devices.FirstOrDefault(d => d.DeviceId == parsedEsp32Id); //
                            if (deviceVm == null)
                            {
                                deviceVm = new DeviceStatusViewModel(parsedEsp32Id); //
                                Esp32Devices.Add(deviceVm); //
                            }
                            deviceVm.LastUpdated = DateTime.UtcNow; //

                            if (statusPayload.Status == "online") //
                            {
                                deviceVm.ConnectionStatus = "在線"; //
                                deviceVm.IpAddress = statusPayload.IP; //
                                _logger.LogInformation("ESP32 設備 {DeviceId} 在線，IP: {IP}", parsedEsp32Id, statusPayload.IP); //
                            }
                            else if (statusPayload.Status == "offline") //
                            {
                                deviceVm.ConnectionStatus = "離線"; //
                                deviceVm.IpAddress = null; //
                                _logger.LogInformation("ESP32 設備 {DeviceId} 離線 (LWT).", parsedEsp32Id); //
                            }
                            else
                            {
                                deviceVm.ConnectionStatus = statusPayload.Status ?? "狀態未知"; //
                            }
                        }
                    }
                    //else if (topic.EndsWith("/led/status")) //
                    //{
                    //    if (string.IsNullOrEmpty(parsedEsp32Id)) return; //

                    //    var ledStatus = JsonSerializer.Deserialize<Esp32LedStatusPayload>(payloadJson); //
                    //    if (ledStatus != null && ledStatus.DeviceId == parsedEsp32Id) //
                    //    {
                    //        var deviceVm = Esp32Devices.FirstOrDefault(d => d.DeviceId == parsedEsp32Id); //
                    //        if (deviceVm == null)
                    //        {
                    //            _logger.LogWarning("收到來自未知 ESP32 {DeviceId} 的 LED 狀態，但設備列表不存在。", parsedEsp32Id); //
                    //            return;
                    //        }

                    //        _logger.LogInformation("收到 ESP32 設備 {DeviceId} LED 狀態回饋: {Message}", parsedEsp32Id, ledStatus.Message); //
                    //        if (ledStatus.Status == "success") //
                    //        {
                    //            if (!string.IsNullOrEmpty(ledStatus.LedState)) //
                    //            {
                    //                deviceVm.IsLedOn = (ledStatus.LedState == "ON"); //
                    //            }
                    //        }
                    //        else
                    //        {
                    //            _logger.LogWarning("ESP32 設備 {DeviceId} LED 操作失敗: {Message}", parsedEsp32Id, ledStatus.Message); //
                    //        }
                    //    }
                    //}

                    else if (topic.EndsWith("/modbus/write/response")) //
                    {
                        _logger.LogInformation("收到 Modbus Write Response on topic {Topic}: {Payload}", topic, payloadJson); //
                        var responseData = JsonSerializer.Deserialize<ModbusWriteResponsePayload>(payloadJson); //

                        if (responseData != null && !string.IsNullOrEmpty(responseData.DeviceId) && responseData.SlaveId > 0) //
                        {
                            if (_isDisposed)
                            {
                                _logger.LogWarning("Skipping Modbus Write Response handling because MainViewModel is disposed. Topic: {Topic}", topic);
                                return;
                            }


                            var homeViewModel = _serviceProvider.GetService<HomeViewModel>();
                            if (homeViewModel != null && responseData.DeviceId != null) //
                            {
                                _logger.LogDebug("Calling HomeViewModel.UpdateDeviceStatusFromMqtt for Write Response. ESP32: {DeviceId}, Slave: {SlaveId}, Status: {Status}",
                                                 responseData.DeviceId, responseData.SlaveId, responseData.Status);
                                homeViewModel.UpdateDeviceStatusFromMqtt( //
                                    responseData.DeviceId, //
                                    responseData.SlaveId,  //
                                    responseData.Status ?? "未知狀態", //
                                    responseData.Message   //
                                );
                            }
                            else
                            {
                                _logger.LogWarning("HomeViewModel not available or DeviceId null in Modbus Write Response. ESP32: {DeviceId}", responseData.DeviceId);
                            }

                        }
                    }
                    else if (topic.EndsWith("/modbus/read/response")) //
                    {
                        _logger.LogInformation("收到 Modbus Read Response on topic {Topic}: {Payload}", topic, payloadJson); //
                        var responseData = JsonSerializer.Deserialize<ModbusReadResponsePayload>(payloadJson); //

                        if (responseData != null && !string.IsNullOrEmpty(responseData.DeviceId)) //
                        {
                            if (_isDisposed)
                            {
                                _logger.LogWarning("Skipping Modbus Read Response handling (before scope creation) because MainViewModel is disposed. Topic: {Topic}", topic);
                                return;
                            }

                            _logger.LogDebug("Attempting to create scope and DbContext for Modbus Read Response. DeviceId: {DeviceId}, SlaveId: {SlaveId}, Address: {Address}, Quantity: {Quantity}",
                                             responseData.DeviceId, responseData.SlaveId, responseData.Address, responseData.Quantity);

                            using var scope = _serviceProvider.CreateScope(); //
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>(); //
                            _logger.LogDebug("DbContext obtained for DeviceId: {DeviceId}, SlaveId: {SlaveId}", responseData.DeviceId, responseData.SlaveId);

                            var deviceInDb = await dbContext.Devices.FirstOrDefaultAsync(d => //
                                d.ControllingEsp32MqttId == responseData.DeviceId && //
                                d.SlaveId == responseData.SlaveId); //

                            if (deviceInDb == null) //
                            {
                                _logger.LogWarning("Modbus 讀取回應：在資料庫中找不到 ESP32 {Esp32Id}, Slave {SlaveId}. 無法更新資料庫或 UI。", //
                                                   responseData.DeviceId, responseData.SlaveId);
                            }
                            else
                            {
                                _logger.LogDebug("Device found in DB. ID: {DbId}, Name: {DeviceName}. Current DB Status: '{DbStatus}', RunCount: {DbRunCount}",
                                                deviceInDb.Id, deviceInDb.Name, deviceInDb.Status, deviceInDb.RunCount);
                                bool dbChanged = false;

                                if (responseData.Status?.ToLower() == "success" && responseData.Data != null) //
                                {
                                    string originalStatus = deviceInDb.Status; //
                                    int originalRunCount = deviceInDb.RunCount; //

                                    if (responseData.Address == ModbusPollingService.STATUS_RELATIVE_ADDRESS && responseData.Quantity == 1 && responseData.Data.Length >= 1) //
                                    {
                                        ushort rawStatus = responseData.Data[0]; //
                                        string newDeviceStatus = ConvertRawModbusStatusToString(rawStatus); //
                                        if (deviceInDb.Status != newDeviceStatus) //
                                        {
                                            _logger.LogInformation("DB Update (Attempt): ESP32 {Esp32Id}, Slave {SlaveId} - Status changing from '{OldStatus}' to '{NewStatus}' (Raw: {RawStatus}) from Addr {Addr}",
                                                                   responseData.DeviceId, responseData.SlaveId, deviceInDb.Status, newDeviceStatus, rawStatus, responseData.Address);
                                            deviceInDb.Status = newDeviceStatus; //
                                            dbChanged = true; //
                                        }
                                    }
                                    else if (responseData.Address == ModbusPollingService.RUNCOUNT_RELATIVE_ADDRESS && responseData.Quantity == 2 && responseData.Data.Length >= 2) //
                                    {
                                        ushort word0 = responseData.Data[0]; //
                                        ushort word1 = responseData.Data[1]; //
                                        uint unsignedRunCount = ((uint)word1 << 16) | word0; //
                                        int newRunCount = (int)unsignedRunCount; //
                                        _logger.LogInformation("RunCount Raw from MQTT: Data[0]={Word0_Hex} (Dec:{Word0_Dec}), Data[1]={Word1_Hex} (Dec:{Word1_Dec})", //
                                                              word0.ToString("X4"), word0, word1.ToString("X4"), word1); //
                                        _logger.LogInformation("RunCount Combined (MSW first): Unsigned={UnsignedVal}, Signed={SignedVal}", //
                                                               unsignedRunCount, newRunCount); //

                                        if (deviceInDb.RunCount != newRunCount) //
                                        {
                                            _logger.LogInformation("DB Update (Attempt): ESP32 {Esp32Id}, Slave {SlaveId} - RunCount changing from {OldRunCount} to {NewRunCount}",
                                                                   responseData.DeviceId, responseData.SlaveId, deviceInDb.RunCount, newRunCount);
                                            deviceInDb.RunCount = newRunCount; //
                                            dbChanged = true; //
                                        }
                                    }

                                    if (dbChanged) //
                                    {
                                        deviceInDb.Timestamp = DateTime.UtcNow; //
                                        _logger.LogInformation("DB Save (Attempt): Saving changes for ESP32 {Esp32Id}, Slave {SlaveId}. New Status: '{NewStatus}', New RunCount: {NewRunCount}",
                                                               responseData.DeviceId, responseData.SlaveId, deviceInDb.Status, deviceInDb.RunCount);
                                        await dbContext.SaveChangesAsync(); //
                                        _logger.LogInformation("DB Save (Success): 成功更新資料庫：ESP32 {Esp32Id}, Slave {SlaveId}. Status is now '{FinalStatus}', RunCount is now {FinalRunCount}.", //
                                                               responseData.DeviceId, responseData.SlaveId, deviceInDb.Status, deviceInDb.RunCount);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("No changes detected in DB for ESP32 {Esp32Id}, Slave {SlaveId} based on MQTT read response.", responseData.DeviceId, responseData.SlaveId);
                                    }
                                }
                                else if (responseData.Status?.ToLower() == "error") //
                                {
                                    _logger.LogError("Modbus 讀取失敗 (ESP32: {Esp32Id}, Slave: {SlaveId}, Addr: {Addr}, Qty: {Qty}): {Message}", //
                                                     responseData.DeviceId, responseData.SlaveId, responseData.Address, responseData.Quantity, responseData.Message); //
                                    if (deviceInDb.Status != "通訊失敗") //
                                    {
                                        _logger.LogInformation("DB Update (Attempt): ESP32 {Esp32Id}, Slave {SlaveId} - Status changing to '通訊失敗' due to read error.", responseData.DeviceId, responseData.SlaveId);
                                        deviceInDb.Status = "通訊失敗"; //
                                        deviceInDb.Timestamp = DateTime.UtcNow; //
                                        await dbContext.SaveChangesAsync(); //
                                        _logger.LogInformation("DB Save (Success): Status for ESP32 {Esp32Id}, Slave {SlaveId} set to '通訊失敗'.", responseData.DeviceId, responseData.SlaveId);
                                        dbChanged = true;
                                    }
                                }

                                // 恢復呼叫 HomeViewModel 更新 UI
                                var homeViewModel = _serviceProvider.GetService<HomeViewModel>();
                                if (homeViewModel != null) //
                                {
                                    _logger.LogDebug("Calling HomeViewModel.UpdateDeviceStatusFromMqtt for Read Response. ESP32: {DeviceId}, Slave: {SlaveId}",
                                                     responseData.DeviceId, responseData.SlaveId);
                                    // 使用 deviceInDb 的最新狀態來更新 UI
                                    string statusForUi = deviceInDb.Status; //
                                    int runCountForUi = deviceInDb.RunCount; //
                                    string? contextMessageForUi = dbChanged ? "資料已從 Modbus 更新" : (responseData.Status?.ToLower() == "error" ? responseData.Message : "資料無變更或讀取成功"); //

                                    homeViewModel.UpdateDeviceStatusFromMqtt( //
                                        responseData.DeviceId, //
                                        responseData.SlaveId, //
                                        statusForUi, //
                                        runCountForUi,  //
                                        contextMessageForUi //
                                    );
                                }
                                else
                                {
                                    _logger.LogWarning("HomeViewModel not available for UI update after Modbus Read Response. ESP32: {DeviceId}", responseData.DeviceId);
                                }

                            }
                        }
                    }
                }
                catch (ObjectDisposedException odEx) //
                {
                    _logger.LogWarning(odEx, "IServiceProvider 或其 Scope 已被釋放，無法處理 MQTT 訊息（可能在資料庫操作期間）。主題: {Topic}。這可能在應用程式關閉期間發生。", topic); //
                }
                catch (DbUpdateException dbEx) //
                {
                    _logger.LogError(dbEx, "資料庫更新時發生錯誤 (DbUpdateException)。主題: {Topic}, Payload: {Payload}", topic, payloadJson); //
                }
                catch (JsonException jsonEx) //
                {
                    _logger.LogError(jsonEx, "反序列化 MQTT payload 失敗. Topic: {Topic}, Payload: {Payload}", topic, payloadJson); //
                }
                catch (Exception ex) //
                {
                    _logger.LogError(ex, "處理 MQTT 訊息時發生未預期錯誤. Topic: {Topic}", topic); //
                }
            });
            return Task.CompletedTask; //
        }

        private static string? ParseDeviceIdFromTopic(string topic) //
        {
            var parts = topic.Split('/'); //
            if (parts.Length >= 2 && parts[0] == "devices") //
            {
                return parts[1]; //
            }
            return null; //
        }

        private static string ConvertRawModbusStatusToString(ushort rawStatus) //
        {
            return rawStatus switch //
            {
                0 => "閒置", //
                1 => "運行中", //
                2 => "故障", //
                _ => $"未知狀態碼 ({rawStatus})", //
            };
        }

        public async Task<bool> SendModbusReadCommandAsync(string? targetEsp32MqttId, byte slaveId, ushort address, byte quantity, byte functionCode) //
        {
            if (string.IsNullOrEmpty(targetEsp32MqttId)) //
            {
                _logger.LogWarning("SendModbusReadCommandAsync: targetEsp32MqttId 不可為空。"); //
                return false; //
            }

            var modbusReadPayload = new //
            {
                slaveId = slaveId, //
                address = address, //
                quantity = quantity, //
                functionCode = functionCode //
            };
            string jsonPayload = JsonSerializer.Serialize(modbusReadPayload); //
            string commandTopic = $"devices/{targetEsp32MqttId}/modbus/read/request"; //

            try
            {
                await _mqttService.PublishAsync(commandTopic, jsonPayload); //
                _logger.LogInformation("已發送 Modbus Read 命令到 {Topic} (SlaveID: {SlaveId}): {Payload}", commandTopic, slaveId, jsonPayload); //
                return true; //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT Modbus Read 命令失敗到 {Topic} (SlaveID: {SlaveId})", commandTopic, slaveId); //
                return false; //
            }
        }

        public async Task<bool> SendModbusWriteCommandAsync(string? targetEsp32MqttId, byte slaveId, ushort address, ushort value) //
        {
            if (string.IsNullOrEmpty(targetEsp32MqttId)) //
            {
                _logger.LogWarning("SendModbusWriteCommandAsync: targetEsp32MqttId 不可為空。"); //
                return false; //
            }

            if (!IsLoggedIn || !CanControlDevice) //
            {
                _logger.LogWarning("SendModbusWriteCommandAsync: 未登入或無權限控制設備。"); //
                MessageBox.Show("未登入或無權限執行 Modbus 操作。", "權限錯誤", MessageBoxButton.OK, MessageBoxImage.Warning); //
                return false; //
            }

            var modbusWritePayload = new //
            {
                slaveId = slaveId, //
                address = address, //
                value = value //
            };
            string jsonPayload = JsonSerializer.Serialize(modbusWritePayload); //
            string commandTopic = $"devices/{targetEsp32MqttId}/modbus/write/request"; //

            try
            {
                await _mqttService.PublishAsync(commandTopic, jsonPayload); //
                _logger.LogInformation("已發送 Modbus Write 命令到 {Topic} (SlaveID: {SlaveId}): {Payload}", commandTopic, slaveId, jsonPayload); //
                return true; //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT Modbus Write 命令失敗到 {Topic} (SlaveID: {SlaveId})", commandTopic, slaveId); //
                MessageBox.Show($"無法發送 Modbus 命令到 ESP32 {targetEsp32MqttId} (SlaveID: {slaveId})，請檢查 MQTT 連線。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error); //
                return false; //
            }
        }


        public void UpdateLoginState()
        {
            var currentUserObject = _authService.GetCurrentUser(); //
            CurrentUser = currentUserObject?.Username; //
            bool oldIsLoggedIn = IsLoggedIn; // 用於判斷是否為登出操作
            IsLoggedIn = currentUserObject != null; //

            if (IsLoggedIn && currentUserObject != null && currentUserObject.PermissionsList != null) //
            {
                CanViewHome = currentUserObject.PermissionsList.Contains(Permission.ViewHome.ToString()) || //
                              currentUserObject.PermissionsList.Contains(Permission.All.ToString()); //
                CanControlDevice = currentUserObject.PermissionsList.Contains(Permission.ControlDevice.ToString()) || //
                                   currentUserObject.PermissionsList.Contains(Permission.All.ToString()); //
                CanAll = currentUserObject.PermissionsList.Contains(Permission.All.ToString()); //

                // 如果登入且擁有控制設備的權限，則啟用 Modbus 輪詢
                // 假設 CanControlDevice 權限足以啟用輪詢。您可以根據需要調整此邏輯或新增特定權限。
                if (CanControlDevice)
                {
                    _logger.LogInformation("用戶已登入且擁有足夠權限。正在啟用 Modbus 輪詢。");
                    _pollingStateService.EnablePolling();
                }
                else
                {
                    _logger.LogInformation("用戶已登入，但缺少 Modbus 輪詢權限。輪詢將保持禁用狀態。");
                    _pollingStateService.DisablePolling(); // 確保權限不足時輪詢是禁用的
                }
            }
            else
            {
                CanViewHome = false; //
                CanControlDevice = false; //
                CanAll = false; //
                Esp32Devices?.Clear(); //
                _mainContentFrame?.Navigate(null); //

                // 如果是從登入狀態變為未登入狀態（例如，登出操作），則禁用 Modbus 輪詢
                if (oldIsLoggedIn && !IsLoggedIn)
                {
                    _logger.LogInformation("用戶已登出或會話結束。正在禁用 Modbus 輪詢。");
                    _pollingStateService.DisablePolling();
                }
                else if (!IsLoggedIn && !_pollingStateService.IsPollingEnabled) // 應用程式啟動時，尚未登入
                {
                    _logger.LogInformation("應用程式啟動，用戶未登入。Modbus 輪詢初始為禁用狀態。");
                    _pollingStateService.DisablePolling(); // 明確禁用
                }
            }

            if (IsLoggedIn && IsHomeSelected && _mainContentFrame != null) //
            {
                if (!(_mainContentFrame.Content is HomePage)) //
                {
                    _ = NavigateHomeAsync(); //
                }
            }
            else if (!IsLoggedIn && _mainContentFrame != null) //
            {
                _mainContentFrame.Navigate(null); //
            }
        }


        public void SetMainContentFrame(Frame frame) //
        {
            _mainContentFrame = frame ?? throw new ArgumentNullException(nameof(frame)); //
            if (IsLoggedIn && IsHomeSelected && _mainContentFrame.Content == null) //
            {
                _ = NavigateHomeAsync(); //
            }
        }

        [RelayCommand]
        private async Task NavigateHomeAsync()
        {
            if (_mainContentFrame != null)
            {
                IsHomeSelected = true;
                // 檢查現有的 DataContext 是否已經是正確的 HomeViewModel 實例
                var existingHomePage = _mainContentFrame.Content as HomePage;
                var homeViewModelFromScope = _serviceProvider.GetService<HomeViewModel>(); // 從 MainViewModel 的作用域獲取

                if (existingHomePage == null || existingHomePage.DataContext != homeViewModelFromScope)
                {
                    var homePage = new HomePage();
                    if (homeViewModelFromScope != null)
                    {
                        homeViewModelFromScope.CanControlDevice = CanControlDevice;
                        homePage.DataContext = homeViewModelFromScope; // 設定正確的實例
                        await homeViewModelFromScope.LoadDevicesAsync();
                    }
                    _mainContentFrame.Navigate(homePage);
                }
                else if (homeViewModelFromScope != null) // 如果頁面已存在且 DataContext 正確，可能仍需刷新
                {
                    homeViewModelFromScope.CanControlDevice = CanControlDevice; // 確保權限正確
                    await homeViewModelFromScope.LoadDevicesAsync(); // 考慮是否需要重新載入
                }
            }
        }


        [RelayCommand]
        private void Logout()
        {
            _logger.LogInformation("執行登出操作。正在禁用 Modbus 輪詢。");
            _pollingStateService.DisablePolling(); // 在登出時明確禁用輪詢
            _authService.Logout(); //
            UpdateLoginState(); //
        }


        [RelayCommand]
        private void ShowLogin() //
        {
            // ShowLogin 成功後會調用 UpdateLoginState，進而根據權限啟用輪詢
            if (App.Host != null) //
            {
                var loginWindow = App.Host.Services.GetRequiredService<LoginWindow>(); //
                loginWindow.Owner = Application.Current.MainWindow; //
                bool? result = loginWindow.ShowDialog(); //

                if (result == true) //
                {
                    UpdateLoginState(); //
                }
            }
        }



        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // 釋放受控資源
                    if (_mqttService is MqttService concreteMqttService) //
                    {
                        concreteMqttService.ApplicationMessageReceivedAsync -= HandleEsp32MqttMessagesAsync; //
                        _logger.LogInformation("MainViewModel disposed, unsubscribed from MQTT messages.");
                    }
                }
                // 釋放非受控資源 (如果有的話)
                _isDisposed = true;
            }
        }



    }
}
