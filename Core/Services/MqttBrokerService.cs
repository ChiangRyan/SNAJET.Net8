using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Server;
using SANJET.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace SANJET.Core.Services
{
    public class MqttBrokerService : IMqttBrokerService, IDisposable
    {
        private readonly MqttServer _mqttServer;
        private readonly ILogger<MqttBrokerService> _logger;
        private bool _disposed;

        public MqttBrokerService(ILogger<MqttBrokerService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var factory = new MqttFactory();

            // 先建立選項
            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(1883)
                .Build();

            // 使用選項建立服務器
            _mqttServer = factory.CreateMqttServer(options);
        }

        public async Task StartAsync()
        {
            try
            {
                // 直接啟動，不需要再傳入參數
                await _mqttServer.StartAsync();
                _logger.LogInformation("MQTT Broker 已啟動，監聽於 0.0.0.0:1883");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "啟動 MQTT Broker 失敗");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                await _mqttServer.StopAsync();
                _logger.LogInformation("MQTT Broker 已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "停止 MQTT Broker 失敗");
                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _mqttServer?.StopAsync().GetAwaiter().GetResult();
                _mqttServer?.Dispose();
                _disposed = true;
                _logger.LogInformation("MQTT Broker 已釋放");
            }
        }
    }
}