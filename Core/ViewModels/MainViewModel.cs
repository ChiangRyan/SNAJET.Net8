// chiangryan/snajet.net8/SNAJET.Net8-8cec974352d783d9832b0a46da16694639d02a11/Core/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore; // 新增，為了 FirstOrDefaultAsync
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;
using MQTTnet;
using SANJET.Core.Constants.Enums;
using SANJET.Core.Interfaces;
using SANJET.Core.Services;
using SANJET.UI.Views.Pages;
using SANJET.UI.Views.Windows;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace SANJET.Core.ViewModels
{
    public class Esp32LedStatusPayload //
    {
        public string? Status { get; set; } //
        public string? Message { get; set; } //
        public string? DeviceId { get; set; } //
        public string? LedState { get; set; }
    }

    public class Esp32OnlineStatusPayload //
    {
        public string? Status { get; set; } //
        public string? IP { get; set; } //
        public string? DeviceId { get; set; } //
    }

    public class ModbusWriteResponsePayload //
    {
        public string? DeviceId { get; set; }
        public byte SlaveId { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
    }
    public class ModbusReadResponsePayload //
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


    public partial class MainViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService; //
        private readonly IMqttService _mqttService; //
        private readonly ILogger<MainViewModel> _logger; //
        private readonly IServiceProvider _serviceProvider; // 新增，用於創建 Scope
        private Frame? _mainContentFrame; //

        [ObservableProperty]
        private string? _currentUser; //

        [ObservableProperty]
        private bool _isLoggedIn; //

        [ObservableProperty]
        private bool _isHomeSelected; //

        [ObservableProperty]
        private bool _canViewHome; //

        [ObservableProperty]
        private bool _canControlDevice; //

        [ObservableProperty]
        private bool _canAll; //

        [ObservableProperty]
        private ObservableCollection<DeviceStatusViewModel> esp32Devices; //

        [ObservableProperty]
        private DeviceStatusViewModel? selectedEsp32Device; //

        public MainViewModel(IAuthenticationService authService, IMqttService mqttService, ILogger<MainViewModel> logger, IServiceProvider serviceProvider) // 新增 IServiceProvider
        {
            _authService = authService;
            _mqttService = mqttService;
            _logger = logger;
            _serviceProvider = serviceProvider; // 保存 IServiceProvider

            this.esp32Devices = new ObservableCollection<DeviceStatusViewModel>();

            UpdateLoginState();
            IsHomeSelected = true;
            _ = InitializeMqttRelatedTasksAsync();
        }

        private async Task InitializeMqttRelatedTasksAsync() //
        {
            try
            {
                _logger.LogInformation("MainViewModel: Assuming MQTT client is being connected/is connected by MqttClientConnectionService."); //

                if (_mqttService is MqttService concreteMqttService)
                {
                    concreteMqttService.ApplicationMessageReceivedAsync += HandleEsp32MqttMessagesAsync;
                }
                else
                {
                    _logger.LogWarning("MainViewModel: _mqttService 不是 MqttService 型別，無法訂閱訊息事件。");
                }

                await _mqttService.SubscribeAsync("devices/+/status"); //
                await _mqttService.SubscribeAsync("devices/+/led/status"); //
                await _mqttService.SubscribeAsync("devices/+/modbus/write/response"); //
                await _mqttService.SubscribeAsync("devices/+/modbus/read/response"); //

                _logger.LogInformation("MainViewModel: Subscribed to MQTT topics for multiple devices including Modbus responses.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainViewModel: MQTT related task initialization failed (subscriptions, etc.).");
            }
        }

        private Task HandleEsp32MqttMessagesAsync(MqttApplicationMessageReceivedEventArgs e) //
        {
            var topic = e.ApplicationMessage.Topic;
            var payloadJson = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _logger.LogDebug("MainViewModel 收到 MQTT: 主題='{Topic}', 内容='{Payload}'", topic, payloadJson);

            string? parsedEsp32Id = ParseDeviceIdFromTopic(topic);

            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    if (topic.EndsWith("/status")) //
                    {
                        // ... (ESP32 上線/離線狀態處理，與之前相同) ...
                        if (string.IsNullOrEmpty(parsedEsp32Id)) return;

                        var statusPayload = JsonSerializer.Deserialize<Esp32OnlineStatusPayload>(payloadJson);
                        if (statusPayload != null && statusPayload.DeviceId == parsedEsp32Id)
                        {
                            var deviceVm = Esp32Devices.FirstOrDefault(d => d.DeviceId == parsedEsp32Id);
                            if (deviceVm == null)
                            {
                                deviceVm = new DeviceStatusViewModel(parsedEsp32Id);
                                Esp32Devices.Add(deviceVm);
                            }
                            deviceVm.LastUpdated = DateTime.UtcNow;

                            if (statusPayload.Status == "online")
                            {
                                deviceVm.ConnectionStatus = "在線";
                                deviceVm.IpAddress = statusPayload.IP;
                                _logger.LogInformation("ESP32 設備 {DeviceId} 在線，IP: {IP}", parsedEsp32Id, statusPayload.IP);
                            }
                            else if (statusPayload.Status == "offline")
                            {
                                deviceVm.ConnectionStatus = "離線";
                                deviceVm.IpAddress = null;
                                _logger.LogInformation("ESP32 設備 {DeviceId} 離線 (LWT).", parsedEsp32Id);
                            }
                            else
                            {
                                deviceVm.ConnectionStatus = statusPayload.Status ?? "狀態未知";
                            }
                        }
                    }
                    else if (topic.EndsWith("/led/status")) //
                    {
                        // ... (LED 狀態處理，與之前相同) ...
                        if (string.IsNullOrEmpty(parsedEsp32Id)) return;

                        var ledStatus = JsonSerializer.Deserialize<Esp32LedStatusPayload>(payloadJson);
                        if (ledStatus != null && ledStatus.DeviceId == parsedEsp32Id)
                        {
                            var deviceVm = Esp32Devices.FirstOrDefault(d => d.DeviceId == parsedEsp32Id);
                            if (deviceVm == null)
                            {
                                _logger.LogWarning("收到來自未知 ESP32 {DeviceId} 的 LED 狀態，但設備列表不存在。", parsedEsp32Id);
                                return;
                            }

                            _logger.LogInformation("收到 ESP32 設備 {DeviceId} LED 狀態回饋: {Message}", parsedEsp32Id, ledStatus.Message);
                            if (ledStatus.Status == "success")
                            {
                                if (!string.IsNullOrEmpty(ledStatus.LedState))
                                {
                                    deviceVm.IsLedOn = (ledStatus.LedState == "ON");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("ESP32 設備 {DeviceId} LED 操作失敗: {Message}", parsedEsp32Id, ledStatus.Message);
                            }
                        }
                    }
                    else if (topic.EndsWith("/modbus/write/response")) //
                    {
                        _logger.LogInformation("收到 Modbus Write Response on topic {Topic}: {Payload}", topic, payloadJson); //
                        var responseData = JsonSerializer.Deserialize<ModbusWriteResponsePayload>(payloadJson); //

                        if (responseData != null && !string.IsNullOrEmpty(responseData.DeviceId) && responseData.SlaveId > 0) // 使用此區塊的 responseData
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var homeViewModel = scope.ServiceProvider.GetService<HomeViewModel>(); //
                            if (homeViewModel != null && responseData.DeviceId != null) // <<-- 添加 responseData.DeviceId != null 檢查
                            {
                                homeViewModel.UpdateDeviceStatusFromMqtt( //
                                    responseData.DeviceId, // 現在是安全的
                                    responseData.SlaveId,  //
                                    responseData.Status ?? "未知狀態", //
                                    responseData.Message   //
                                );
                            }
                        }
                    }
                    else if (topic.EndsWith("/modbus/read/response")) //
                    {
                        _logger.LogInformation("收到 Modbus Read Response on topic {Topic}: {Payload}", topic, payloadJson); //
                        var responseData = JsonSerializer.Deserialize<ModbusReadResponsePayload>(payloadJson); //

                        if (responseData != null && responseData.Status?.ToLower() == "success" && responseData.Data != null && !string.IsNullOrEmpty(responseData.DeviceId)) //
                        {
                            using var scope = _serviceProvider.CreateScope(); // 使用注入的 IServiceProvider
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>(); //
                            var deviceInDb = await dbContext.Devices.FirstOrDefaultAsync(d => //
                                d.ControllingEsp32MqttId == responseData.DeviceId && //
                                d.SlaveId == responseData.SlaveId); //

                            if (deviceInDb != null) //
                            {
                                bool changed = false;
                                if (responseData.Data.Length >= 1)
                                {
                                    ushort rawStatus = responseData.Data[0]; //
                                    string newDeviceStatus = ConvertRawModbusStatusToString(rawStatus); // <<-- 方法定義在下面
                                    if (deviceInDb.Status != newDeviceStatus)
                                    {
                                        deviceInDb.Status = newDeviceStatus; //
                                        changed = true;
                                        _logger.LogInformation("DB Update: ESP32 {Esp32Id}, Slave {SlaveId} - Status updated to {Status}", //
                                                               responseData.DeviceId, responseData.SlaveId, newDeviceStatus);
                                    }
                                }
                                if (responseData.Data.Length >= 2) // 假設 RunCount 在第二個數據 (Data[1])
                                {
                                    int newRunCount = responseData.Data[1];
                                    if (deviceInDb.RunCount != newRunCount)
                                    {
                                        deviceInDb.RunCount = newRunCount; //
                                        changed = true;
                                        _logger.LogInformation("DB Update: ESP32 {Esp32Id}, Slave {SlaveId} - RunCount updated to {RunCount}", //
                                                               responseData.DeviceId, responseData.SlaveId, newRunCount);
                                    }
                                }

                                if (changed)
                                {
                                    await dbContext.SaveChangesAsync(); //
                                    _logger.LogInformation("成功更新資料庫：ESP32 {Esp32Id}, Slave {SlaveId}", responseData.DeviceId, responseData.SlaveId); //

                                    var homeViewModel = scope.ServiceProvider.GetService<HomeViewModel>(); //
                                    if (homeViewModel != null && responseData.DeviceId != null) // <<-- 添加 responseData.DeviceId != null 檢查
                                    {
                                        homeViewModel.UpdateDeviceStatusFromMqtt( //
                                            responseData.DeviceId, //
                                            responseData.SlaveId, //
                                            deviceInDb.Status, //
                                            "資料已從 Modbus 更新" //
                                        );
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Modbus 讀取回應：在資料庫中找不到 ESP32 {Esp32Id}, Slave {SlaveId}", //
                                                   responseData.DeviceId, responseData.SlaveId);
                            }
                        }

                        else if (responseData != null && responseData.Status?.ToLower() == "error") //
                        {
                            _logger.LogError("Modbus 讀取失敗 (ESP32: {Esp32Id}, Slave: {SlaveId}): {Message}", //
                                             responseData.DeviceId, responseData.SlaveId, responseData.Message);
                            using var scope = _serviceProvider.CreateScope(); // 使用注入的 IServiceProvider
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>(); //
                            var deviceInDb = await dbContext.Devices.FirstOrDefaultAsync(d => //
                                d.ControllingEsp32MqttId == responseData.DeviceId && //
                                d.SlaveId == responseData.SlaveId); //
                            if (deviceInDb != null && deviceInDb.Status != "通訊失敗")
                            {
                                deviceInDb.Status = "通訊失敗"; //
                                await dbContext.SaveChangesAsync(); //
                                _logger.LogInformation("DB Update: ESP32 {Esp32Id}, Slave {SlaveId} - Status set to 通訊失敗 due to read error", //
                                                       responseData.DeviceId, responseData.SlaveId);

                                var homeViewModel = scope.ServiceProvider.GetService<HomeViewModel>(); //
                                homeViewModel?.UpdateDeviceStatusFromMqtt(responseData.DeviceId, responseData.SlaveId, "通訊失敗", responseData.Message); //
                            }
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "反序列化 MQTT payload 失敗. Topic: {Topic}, Payload: {Payload}", topic, payloadJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "處理 MQTT 訊息時發生錯誤. Topic: {Topic}", topic);
                }
            });
            return Task.CompletedTask;
        }

        private string? ParseDeviceIdFromTopic(string topic) //
        {
            var parts = topic.Split('/');
            if (parts.Length >= 2 && parts[0] == "devices")
            {
                return parts[1];
            }
            return null;
        }

        // **新增 ConvertRawModbusStatusToString 方法**
        private string ConvertRawModbusStatusToString(ushort rawStatus) //
        {
            // 根據您的 Modbus 設備規格實現轉換邏輯
            return rawStatus switch
            {
                0 => "閒置",
                1 => "運行中",
                2 => "故障",
                // 添加更多狀態映射...
                _ => $"未知狀態碼 ({rawStatus})",
            };
        }

        // ... (ToggleLed, SendModbusWriteCommandAsync, UpdateLoginState, SetMainContentFrame, NavigateHomeAsync, Logout, ShowLogin 方法與之前範例一致)
        // SendModbusReadCommandAsync 方法已在上面提供
        public async Task<bool> SendModbusReadCommandAsync(string? targetEsp32MqttId, byte slaveId, ushort address, byte quantity, byte functionCode) //
        {
            if (string.IsNullOrEmpty(targetEsp32MqttId))
            {
                _logger.LogWarning("SendModbusReadCommandAsync: targetEsp32MqttId 不可為空。");
                return false;
            }
            // 考慮是否真的需要登入才能執行背景輪詢發起的讀取
            // if (!IsLoggedIn) 
            // {
            //     _logger.LogWarning("SendModbusReadCommandAsync: 未登入，取消讀取命令。");
            //     return false;
            // }

            var modbusReadPayload = new
            {
                slaveId = slaveId,
                address = address,
                quantity = quantity,
                functionCode = functionCode
            };
            string jsonPayload = JsonSerializer.Serialize(modbusReadPayload);
            string commandTopic = $"devices/{targetEsp32MqttId}/modbus/read/request";

            try
            {
                await _mqttService.PublishAsync(commandTopic, jsonPayload);
                _logger.LogInformation("已發送 Modbus Read 命令到 {Topic} (SlaveID: {SlaveId}): {Payload}", commandTopic, slaveId, jsonPayload);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT Modbus Read 命令失敗到 {Topic} (SlaveID: {SlaveId})", commandTopic, slaveId);
                return false;
            }
        }

        public async Task<bool> SendModbusWriteCommandAsync(string? targetEsp32MqttId, byte slaveId, ushort address, ushort value) //
        {
            if (string.IsNullOrEmpty(targetEsp32MqttId))
            {
                _logger.LogWarning("SendModbusWriteCommandAsync: targetEsp32MqttId 不可為空。");
                return false;
            }

            if (!IsLoggedIn || !CanControlDevice)
            {
                _logger.LogWarning("SendModbusWriteCommandAsync: 未登入或無權限控制設備。");
                MessageBox.Show("未登入或無權限執行 Modbus 操作。", "權限錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var modbusWritePayload = new
            {
                slaveId = slaveId,
                address = address,
                value = value
            };
            string jsonPayload = JsonSerializer.Serialize(modbusWritePayload);
            string commandTopic = $"devices/{targetEsp32MqttId}/modbus/write/request";

            try
            {
                await _mqttService.PublishAsync(commandTopic, jsonPayload);
                _logger.LogInformation("已發送 Modbus Write 命令到 {Topic} (SlaveID: {SlaveId}): {Payload}", commandTopic, slaveId, jsonPayload);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT Modbus Write 命令失敗到 {Topic} (SlaveID: {SlaveId})", commandTopic, slaveId);
                MessageBox.Show($"無法發送 Modbus 命令到 ESP32 {targetEsp32MqttId} (SlaveID: {slaveId})，請檢查 MQTT 連線。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        public void UpdateLoginState() //
        {
            var currentUserObject = _authService.GetCurrentUser(); //
            CurrentUser = currentUserObject?.Username; //
            IsLoggedIn = currentUserObject != null; //

            if (IsLoggedIn && currentUserObject != null && currentUserObject.PermissionsList != null) //
            {
                CanViewHome = currentUserObject.PermissionsList.Contains(Permission.ViewHome.ToString()) || //
                              currentUserObject.PermissionsList.Contains(Permission.All.ToString()); //
                CanControlDevice = currentUserObject.PermissionsList.Contains(Permission.ControlDevice.ToString()) || //
                                   currentUserObject.PermissionsList.Contains(Permission.All.ToString()); //
                CanAll = currentUserObject.PermissionsList.Contains(Permission.All.ToString()); //
            }
            else
            {
                CanViewHome = false; //
                CanControlDevice = false; //
                CanAll = false; //
                if (Esp32Devices != null) Esp32Devices.Clear(); //
                _mainContentFrame?.Navigate(null); //
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
        private async Task NavigateHomeAsync() //
        {
            if (_mainContentFrame != null) //
            {
                IsHomeSelected = true; //
                if (!(_mainContentFrame.Content is HomePage)) //
                {
                    var homePage = new HomePage(); //
                    if (App.Host != null) //
                    {
                        var homeViewModel = App.Host.Services.GetService<HomeViewModel>(); //
                        if (homeViewModel != null) //
                        {
                            homeViewModel.CanControlDevice = CanControlDevice; //
                            homePage.DataContext = homeViewModel; //
                            await homeViewModel.LoadDevicesAsync(); //
                        }
                    }
                    _mainContentFrame.Navigate(homePage); //
                }
            }
        }
        [RelayCommand]
        private void Logout() //
        {
            _authService.Logout(); //
            UpdateLoginState(); //
        }
        [RelayCommand]
        private void ShowLogin() //
        {
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

    }
}