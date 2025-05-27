

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
    }


}