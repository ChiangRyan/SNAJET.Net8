using MQTTnet;
using MQTTnet.Client; // 確保 MqttClientOptionsBuilder 等被正確引用
using MQTTnet.Extensions.ManagedClient; // 如果未來考慮使用 ManagedClient
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;

namespace SANJET.Core.Services
{
    public class MqttService : IMqttService
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _options;
        private readonly ILogger<MqttService> _logger;

        // 事件，用於通知其他組件收到了 MQTT 訊息
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;


        public MqttService(ILogger<MqttService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();
            _options = new MqttClientOptionsBuilder()
                .WithTcpServer("localhost", 1883) // 連接到內建 Broker
                .WithCleanSession() // 建議設定 CleanSession
                .Build();

            // 註冊訊息接收處理器
            _mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        }

        private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _logger.LogInformation("接收到 MQTT 訊息: 主題 '{Topic}', 内容 '{Payload}'", topic, payload);

            // 觸發事件，讓訂閱者處理
            return ApplicationMessageReceivedAsync?.Invoke(e) ?? Task.CompletedTask;
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
                    throw; // 保留拋出異常，讓調用者知道連接失敗
                }
            }
        }

        public async Task PublishAsync(string topic, string payload)
        {
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("發佈 MQTT 訊息失敗：客戶端未連接。主題: {Topic}", topic);
                // 可以考慮拋出異常或返回一個失敗的狀態
                // throw new InvalidOperationException("MQTT client is not connected.");
                return;
            }
            try
            {
                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload) // payload 已經是 string
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce) // 建議 QoS
                    .Build();

                await _mqttClient.PublishAsync(applicationMessage);
                _logger.LogInformation("已發送 MQTT 訊息到主題 {Topic}: {Payload}", topic, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT 訊息失敗 主題: {Topic}", topic);
                throw; // 保留拋出異常
            }
        }

        public async Task SubscribeAsync(string topic)
        {
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("訂閱主題失敗：MQTT 客戶端未連接。主題: {Topic}", topic);
                return;
            }
            try
            {
                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .Build();
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topicFilter)
                    .Build();

                var result = await _mqttClient.SubscribeAsync(subscribeOptions);
                foreach (var subscription in result.Items)
                {
                    _logger.LogInformation("已成功訂閱主題 '{Topic}'. Result: {ResultCode}", subscription.TopicFilter.Topic, subscription.ResultCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "訂閱主題失敗 '{Topic}'", topic);
            }
        }

        public async Task UnsubscribeAsync(string topic)
        {
            if (!_mqttClient.IsConnected)
            {
                _logger.LogWarning("取消訂閱主題失敗：MQTT 客戶端未連接。主題: {Topic}", topic);
                return;
            }
            try
            {
                await _mqttClient.UnsubscribeAsync(topic);
                _logger.LogInformation("已取消訂閱主題 '{Topic}'.", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消訂閱主題失敗 '{Topic}'", topic);
            }
        }
    }
}