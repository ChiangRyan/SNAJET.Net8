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

        public ModbusPollingService(ILogger<ModbusPollingService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Modbus Polling Service is starting.");
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // 等待其他服務啟動

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Modbus Polling Service running a cycle at: {time}", DateTimeOffset.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var mainViewModel = scope.ServiceProvider.GetRequiredService<MainViewModel>();

                        var devicesToPoll = await dbContext.Devices
                            .Where(d => !string.IsNullOrEmpty(d.ControllingEsp32MqttId) && d.IsOperational)
                            .ToListAsync(stoppingToken);

                        if (!devicesToPoll.Any())
                        {
                            _logger.LogInformation("No devices configured for Modbus polling in this cycle.");
                            await Task.Delay(_pollingInterval, stoppingToken);
                            continue;
                        }

                        foreach (var device in devicesToPoll)
                        {
                            if (stoppingToken.IsCancellationRequested) break;

                            if (string.IsNullOrEmpty(device.ControllingEsp32MqttId))
                            {
                                _logger.LogWarning("Device ID {DbDeviceId} (Slave {SlaveId}) is missing ControllingEsp32MqttId, skipping poll.", device.Id, device.SlaveId);
                                continue;
                            }

                            // 1. 讀取狀態 (Status)
                            _logger.LogInformation("Polling Status for ESP32: {Esp32Id}, Slave: {SlaveId}, Address: {Address}",
                                device.ControllingEsp32MqttId, device.SlaveId, STATUS_RELATIVE_ADDRESS);
                            await mainViewModel.SendModbusReadCommandAsync(
                                device.ControllingEsp32MqttId,
                                (byte)device.SlaveId,
                                STATUS_RELATIVE_ADDRESS,
                                1, // 讀取1個暫存器 (16-bit) for Status
                                3  // 功能碼 3 (Read Holding Registers) - 假設狀態是保持暫存器
                            );
                            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken); // 請求間延遲

                            if (stoppingToken.IsCancellationRequested) break;

                            // 2. 讀取運轉次數 (RunCount)
                            _logger.LogInformation("Polling RunCount for ESP32: {Esp32Id}, Slave: {SlaveId}, Address: {Address}",
                                device.ControllingEsp32MqttId, device.SlaveId, RUNCOUNT_RELATIVE_ADDRESS);
                            await mainViewModel.SendModbusReadCommandAsync(
                                device.ControllingEsp32MqttId,
                                (byte)device.SlaveId,
                                RUNCOUNT_RELATIVE_ADDRESS,
                                2, // 讀取2個連續的暫存器 (16-bit each) for RunCount (32-bit)
                                3  // 功能碼 3 - 假設 RunCount 也是保持暫存器
                            );
                            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken); // 請求間延遲
                        }
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