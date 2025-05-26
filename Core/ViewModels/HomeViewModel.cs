using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore; // 
using SANJET.Core.Models;          //
using System.Collections.ObjectModel;
using System.Linq;                 // 
using System.Threading.Tasks;      // 
namespace SANJET.Core.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext; // 
        public HomeViewModel(AppDbContext dbContext) // <--- 修改建構函數
        {
            _dbContext = dbContext; // 
            // 初始化設備列表 - 這裡需要根據您的實際需求來實現
            Devices = [];
            // LoadDevices(); // 改為異步加載或在適當時機調用
        }

        [ObservableProperty]
        private ObservableCollection<DeviceViewModel> devices = [];

        [ObservableProperty]
        private bool canControlDevice;

        public async Task LoadDevicesAsync()
        {
            Devices.Clear();
            var devicesFromDb = await _dbContext.Devices.ToListAsync();
            foreach (var device in devicesFromDb)
            {
                Devices.Add(new DeviceViewModel
                {
                    // Id = device.Id, // 如果 DeviceViewModel 也需要 Id
                    Name = device.Name,
                    IpAddress = device.IpAddress,
                    SlaveId = device.SlaveId,
                    Status = device.Status,
                    IsOperational = device.IsOperational,
                    RunCount = device.RunCount
                    // 確保 DeviceViewModel 中的屬性與 Device 模型對應
                });
            }
        }

        // 示例：保存設備變更的方法 (例如，當名稱或 IsOperational 改變時)
        // 您可能需要在 DeviceViewModel 中實現屬性變更通知，然後觸發保存
        [RelayCommand]
        private async Task SaveDeviceChangesAsync(DeviceViewModel deviceVm)
        {
            if (deviceVm == null) return;

            // 假設 DeviceViewModel 有一個 Id 或者可以通過其他唯一標識符找到對應的 Device
            // 這裡假設我們通過 Name (如果 Name 是唯一的) 或需要一個 Id
            // 為了簡化，我們假設可以通過某些方式找到或更新 Device
            // 更完善的做法是 DeviceViewModel 也包含 Id

            var deviceInDb = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Name == deviceVm.Name /* 或 d.Id == deviceVm.Id */);
            if (deviceInDb != null)
            {
                deviceInDb.Name = deviceVm.Name;
                deviceInDb.IpAddress = deviceVm.IpAddress; // 通常 IP 和 SlaveId 不應隨意更改
                deviceInDb.SlaveId = deviceVm.SlaveId;
                deviceInDb.Status = deviceVm.Status;
                deviceInDb.IsOperational = deviceVm.IsOperational;
                deviceInDb.RunCount = deviceVm.RunCount;
                
                _dbContext.Devices.Update(deviceInDb);
            }
            else
            {
                // 如果是新設備，則添加到資料庫
                var newDevice = new Device
                {
                    Name = deviceVm.Name,
                    IpAddress = deviceVm.IpAddress,
                    SlaveId = deviceVm.SlaveId,
                    Status = deviceVm.Status,
                    IsOperational = deviceVm.IsOperational,
                    RunCount = deviceVm.RunCount
                };
                await _dbContext.Devices.AddAsync(newDevice);
            }
            await _dbContext.SaveChangesAsync();
        }

        // 您需要在適當的時機（例如頁面載入時）呼叫 LoadDevicesAsync
        // 例如，在 MainViewModel 的 NavigateHome 中，獲取 HomeViewModel 後呼叫它
        // 或者在 HomeViewModel 的建構函數之後異步呼叫
    }

    // 如果 DeviceViewModel 與 Device 結構非常相似，可以考慮重用或繼承
    // 或者確保它們之間的映射是正確的
    // public partial class DeviceViewModel : ObservableObject ...


    // 設備 ViewModel（如果不存在的話）
    public partial class DeviceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string ipAddress = string.Empty;

        [ObservableProperty]
        private int slaveId;

        [ObservableProperty]
        private string status = "閒置";

        [ObservableProperty]
        private bool isOperational = true;

        [ObservableProperty]
        private int runCount;


        [RelayCommand]
        private void Start()
        {
            // 啟動設備邏輯
            Status = "運行中";
        }

        [RelayCommand]
        private void Stop()
        {
            // 停止設備邏輯
            Status = "閒置";
        }

        [RelayCommand]
        private void Record()
        {
            // 記錄邏輯
        }
    }
}