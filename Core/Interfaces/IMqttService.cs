using System.Threading.Tasks;
using MQTTnet; // 若介面中也定義事件，則需要此 using

namespace SANJET.Core.Interfaces
{
    public interface IMqttBrokerService
    {
        Task StartAsync();
        Task StopAsync();
    }

    public interface IMqttService
    {
        Task ConnectAsync();
        Task PublishAsync(string topic, string payload);
        Task SubscribeAsync(string topic);       // 新增
        Task UnsubscribeAsync(string topic);     // 新增 (可選，但建議)

        // 如果希望 ViewModel 透過介面訂閱事件，可以這樣宣告：
        // event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;
        // 然後 MqttService.cs 中的事件需要明確實作此介面事件。
        // 但目前 MainViewModel.cs 是透過轉型來訂閱實作類別的事件。
    }
}