
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces; // For IMqttService
using SANJET.Core.ViewModels; // For MainViewModel (to send MQTT)
using System;
using System.Linq;
using System.Text.Json; // 確保 JsonSerializer 可用
using System.Threading;
using System.Threading.Tasks;
using SANJET.Core; // For AppDbContext
using Microsoft.EntityFrameworkCore; // For ToListAsync, FirstOrDefaultAsync

namespace SANJET.Core.Services
{
    public class ModbusPollingService : BackgroundService
    {
        private readonly ILogger<ModbusPollingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30); // 每 30 秒輪詢一次 (可調整)

        public ModbusPollingService(ILogger<ModbusPollingService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Modbus Polling Service is starting.");

            // 等待應用程式完全啟動，特別是 MainViewModel 和 MQTT 服務
            // 這是一個簡單的延遲，更穩健的方式可能是等待某個應用程式就緒事件
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Modbus Polling Service is running a cycle at: {time}", DateTimeOffset.Now);

                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var mainViewModel = scope.ServiceProvider.GetRequiredService<MainViewModel>();

                    // 獲取所有需要輪詢的資料庫設備
                    var devicesToPoll = await dbContext.Devices
                        .Where(d => !string.IsNullOrEmpty(d.ControllingEsp32MqttId) && d.IsOperational)
                        .ToListAsync(stoppingToken); // 使用非同步版本並傳遞 CancellationToken

                    if (!devicesToPoll.Any())
                    {
                        _logger.LogInformation("No devices configured for Modbus polling in this cycle.");
                        await Task.Delay(_pollingInterval, stoppingToken);
                        continue;
                    }

                    foreach (var device in devicesToPoll)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        // 您需要定義要讀取的暫存器位址和數量
                        // 範例：讀取從站設備的 4 個保持暫存器，從相對位址 100 開始
                        ushort relativeReadAddress = 1; // 假設狀態和計數器從這裡開始
                        byte quantityToRead = 2;      // 假設讀取 4 個字 (ushort)
                        byte functionCode = 3;        // 功能碼 3 (Read Holding Registers)

                        _logger.LogInformation("Polling Modbus data for ESP32: {Esp32Id}, Slave: {SlaveId}, Address: {Address}, Quantity: {Quantity}, FC: {FC}",
                            device.ControllingEsp32MqttId, device.SlaveId, relativeReadAddress, quantityToRead, functionCode);

                        // 呼叫 MainViewModel 中的 SendModbusReadCommandAsync 方法
                        // 注意：mainViewModel.SendModbusReadCommandAsync 方法的第一個參數是 string?
                        // device.ControllingEsp32MqttId 也是 string?，直接傳遞即可
                        if (!string.IsNullOrEmpty(device.ControllingEsp32MqttId))
                        {
                            await mainViewModel.SendModbusReadCommandAsync(
                                device.ControllingEsp32MqttId,
                                (byte)device.SlaveId,
                                relativeReadAddress,
                                quantityToRead,
                                functionCode
                            );
                        }
                        else
                        {
                            _logger.LogWarning("Device ID {DeviceId} (Slave {SlaveId}) is missing ControllingEsp32MqttId, skipping poll.", device.Id, device.SlaveId);
                        }


                        // 請求之間短暫延遲，避免淹沒 ESP32 或網路
                        // 如果輪詢的設備很多，這個延遲很重要
                        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Modbus Polling Service is stopping due to cancellation.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in Modbus Polling Service execution cycle.");
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
            }
            _logger.LogInformation("Modbus Polling Service has stopped.");
        }
    }
}