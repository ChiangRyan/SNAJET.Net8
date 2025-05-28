// 建議放置於 SANJET.Core.Services 或新的 SANJET.Core.Hosting 命名空間下
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SANJET.Core.Services // 或 SANJET.Core.Hosting
{
    public class MqttClientConnectionService : IHostedService
    {
        private readonly IMqttService _mqttService;
        private readonly ILogger<MqttClientConnectionService> _logger;

        public MqttClientConnectionService(IMqttService mqttService, ILogger<MqttClientConnectionService> logger)
        {
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 背景服務啟動時，嘗試連接 MQTT 客戶端
                await _mqttService.ConnectAsync();
                _logger.LogInformation("MqttClientConnectionService: MQTT client connected successfully by background service.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MqttClientConnectionService: Failed to connect MQTT client.");
                // 這裡可以根據需求考慮重試邏輯
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // 當應用程式關閉時，IMqttService (若其內部 IMqttClient 是 IDisposable) 會被釋放。
            // 如果 MqttService 需要明確的斷線邏輯，應在 IMqttService 介面中增加 DisconnectAsync 方法。
            _logger.LogInformation("MqttClientConnectionService is stopping. MQTT client will handle disconnection or be disposed.");
            return Task.CompletedTask;
        }
    }
}