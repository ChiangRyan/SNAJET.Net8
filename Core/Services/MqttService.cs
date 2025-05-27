
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; 
using SANJET.Core.Interfaces;
using MQTTnet.Client;


namespace SANJET.Core.Services
{

    public class MqttService : IMqttService
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _options;
        private readonly ILogger<MqttService> _logger;

        public MqttService(ILogger<MqttService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();
            _options = new MqttClientOptionsBuilder()
                .WithTcpServer("localhost", 1883) // 連接到內建 Broker
                .Build();
        }

        public async Task ConnectAsync()
        {
            if (!_mqttClient.IsConnected)
            {
                try
                {
                    await _mqttClient.ConnectAsync(_options);
                    _logger.LogInformation("MQTT 客戶端已連接到本機 Broker");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "連接到 MQTT Broker 失敗");
                    throw;
                }
            }
        }

        public async Task PublishAsync(string topic, string payload)
        {
            try
            {
                await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(System.Text.Encoding.UTF8.GetBytes(payload))
                    .Build());
                _logger.LogInformation("已發送 MQTT 訊息到主題 {Topic}: {Payload}", topic, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT 訊息失敗");
                throw;
            }
        }
    }
}
