// Core/Services/ModbusPollingService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using SANJET.Core.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SANJET.Core;
using Microsoft.EntityFrameworkCore;

namespace SANJET.Core.Services
{
    public class ModbusPollingService : BackgroundService
    {
        private readonly ILogger<ModbusPollingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10); // 輪詢間隔

        public const ushort STATUS_RELATIVE_ADDRESS = 1;  // 狀態的相對位址
        public const ushort RUNCOUNT_RELATIVE_ADDRESS = 10; // 運轉次數的起始相對位址
        private readonly IPollingStateService _pollingStateService; // 新增欄位
        private readonly ManualResetEventSlim _runPollingSignal = new ManualResetEventSlim(false); // 用於控制輪詢迴圈的啟動/暫停

        public ModbusPollingService(ILogger<ModbusPollingService> logger,
                                IServiceProvider serviceProvider,
                                IPollingStateService pollingStateService) // 
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _pollingStateService = pollingStateService; // 

            // 訂閱狀態變更事件
            _pollingStateService.PollingStateChanged += OnPollingStateChanged;
            // 設定初始訊號狀態
            if (_pollingStateService.IsPollingGloballyEnabled)
            {
                _runPollingSignal.Set();
            }
        }

        private void OnPollingStateChanged(object? sender, EventArgs e)
        {
            if (_pollingStateService.IsPollingGloballyEnabled)
            {
                _logger.LogInformation("輪詢服務: 收到啟用輪詢的訊號。");
                _runPollingSignal.Set(); // 發送訊號，允許輪詢迴圈執行
            }
            else
            {
                _logger.LogInformation("輪詢服務: 收到停用輪詢的訊號。");
                _runPollingSignal.Reset(); // 重置訊號，使輪詢迴圈等待
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Modbus 輪詢服務已啟動，等待輪詢許可...");

            // 註冊一個當服務停止時取消 ManualResetEventSlim 等待的方法
            stoppingToken.Register(() => _runPollingSignal.Set());

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // 等待 _runPollingSignal 被設定，或者服務被要求停止
                    // Wait 將阻塞執行緒，直到 ManualResetEventSlim 被 Set()，或 CancellationToken 被取消
                    try
                    {
                        _runPollingSignal.Wait(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Modbus 輪詢服務等待被取消 (可能由於應用程式關閉)。");
                        break; // 退出迴圈
                    }


                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Modbus 輪詢服務準備停止 (外部取消)。");
                        break;
                    }

                    // 再次確認輪詢是否真的被允許 (防止競爭條件)
                    if (!_pollingStateService.IsPollingGloballyEnabled)
                    {
                        _logger.LogInformation("Modbus 輪詢服務：輪詢許可已撤銷，暫停當前週期。");
                        // 短暫延遲後繼續等待訊號，避免 CPU 空轉
                        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Modbus 輪詢服務正在執行一個輪詢週期: {time}", DateTimeOffset.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var mainViewModel = scope.ServiceProvider.GetRequiredService<MainViewModel>(); // MainViewModel 也是 Scoped

                        var devicesToPoll = await dbContext.Devices
                            .Where(d => !string.IsNullOrEmpty(d.ControllingEsp32MqttId) && d.IsOperational)
                            .ToListAsync(stoppingToken);

                        if (!devicesToPoll.Any())
                        {
                            _logger.LogInformation("在本輪詢週期中沒有設定為 Modbus 輪詢的設備。");
                            await Task.Delay(_pollingInterval, stoppingToken); // 仍然等待一個間隔
                            continue;
                        }

                        foreach (var device in devicesToPoll)
                        {
                            if (stoppingToken.IsCancellationRequested) break;
                            if (!_pollingStateService.IsPollingGloballyEnabled) // 再次檢查，允許中途停止
                            {
                                _logger.LogInformation("輪詢在設備處理期間被停用。");
                                break; // 中斷當前設備列表的輪詢
                            }


                            // ... (原有的輪詢單個設備的邏輯, 確保 Task.Delay 使用 stoppingToken) ...
                            _logger.LogInformation("輪詢狀態 ESP32: {Esp32Id}, Slave: {SlaveId}, Address: {Address}",
                                device.ControllingEsp32MqttId, device.SlaveId, STATUS_RELATIVE_ADDRESS);
                            await mainViewModel.SendModbusReadCommandAsync(
                                device.ControllingEsp32MqttId,
                                (byte)device.SlaveId,
                                STATUS_RELATIVE_ADDRESS,
                                1, 3);
                            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);

                            if (stoppingToken.IsCancellationRequested || !_pollingStateService.IsPollingGloballyEnabled) break;

                            _logger.LogInformation("輪詢運轉次數 ESP32: {Esp32Id}, Slave: {SlaveId}, Address: {Address}",
                                device.ControllingEsp32MqttId, device.SlaveId, RUNCOUNT_RELATIVE_ADDRESS);
                            await mainViewModel.SendModbusReadCommandAsync(
                                device.ControllingEsp32MqttId,
                                (byte)device.SlaveId,
                                RUNCOUNT_RELATIVE_ADDRESS,
                                2, 3);
                            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                        }
                    }
                    // 只有在輪詢實際執行後才進行完整間隔的等待
                    if (_pollingStateService.IsPollingGloballyEnabled && !stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(_pollingInterval, stoppingToken);
                    }
                    else if (!stoppingToken.IsCancellationRequested) // 如果只是暫停了，則短暫等待
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Modbus 輪詢服務 ExecuteAsync 迴圈被取消。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Modbus 輪詢服務 ExecuteAsync 迴圈中發生未處理的異常。");
            }
            finally
            {
                _pollingStateService.PollingStateChanged -= OnPollingStateChanged; // 取消訂閱
                _logger.LogInformation("Modbus 輪詢服務已停止。");
            }
        }
    }
}