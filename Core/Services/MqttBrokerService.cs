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
            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(1883)
                .Build();
            _mqttServer = factory.CreateMqttServer(options); // 修正 CS1501：傳入 options
        }

        public async Task StartAsync()
        {
            try
            {
                var options = new MqttServerOptionsBuilder()
                    .WithDefaultEndpoint()
                    .WithDefaultEndpointPort(1883) // 使用預設 MQTT 端口
                    .Build();

                await _mqttServer.StartAsync(options);
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