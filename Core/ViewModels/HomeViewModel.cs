using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore; 
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.Models;          
using System.Collections.ObjectModel;


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
                var deviceVm = new DeviceViewModel(this) // <--- 傳遞 HomeViewModel 實例
                {
                    Id = device.Id, // <--- 儲存 Id
                    Name = device.Name,
                    IpAddress = device.IpAddress,
                    SlaveId = device.SlaveId,
                    Status = device.Status,
                    IsOperational = device.IsOperational,
                    RunCount = device.RunCount,
                    OriginalName = device.Name // 初始化 OriginalName
                };
                Devices.Add(deviceVm);
            }
        }

        // 實現儲存變更到特定設備的方法
        public async Task SaveChangesToDeviceAsync(DeviceViewModel deviceVm)
        {
            if (deviceVm == null) return;

            var deviceInDb = await _dbContext.Devices.FindAsync(deviceVm.Id); // 使用 Id 查找
            if (deviceInDb != null)
            {
                // 只更新需要變更的欄位，此處主要示範名稱
                deviceInDb.Name = deviceVm.Name;
                // 如果其他屬性也允許在 DeviceViewModel 中直接修改並保存，也一併更新
                // deviceInDb.IsOperational = deviceVm.IsOperational;
                // ... 其他屬性 ...

                _dbContext.Devices.Update(deviceInDb);
                await _dbContext.SaveChangesAsync();

                // 更新成功後，可以考慮更新 deviceVm 的 OriginalName，使其與當前 Name 一致
                deviceVm.OriginalName = deviceVm.Name;
            }
            // 可以加入日誌記錄或錯誤處理
        }

        // 原來的 SaveDeviceChangesAsync 可以保留或移除，取決於是否有批量儲存的需求
        // [RelayCommand]
        // private async Task SaveDeviceChangesAsync(DeviceViewModel deviceVm) ...


    }


    // 設備 ViewModel（如果不存在的話）
    public partial class DeviceViewModel : ObservableObject
    {
        private readonly HomeViewModel _homeViewModel; // 用於回呼儲存操作

        [ObservableProperty]
        private int id; // 加入 Id 以便於資料庫操作

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

        [ObservableProperty]
        private bool isEditingName = false; // 控制是否處於名稱編輯模式


        // 建構函數，接收 HomeViewModel 實例
        public DeviceViewModel(HomeViewModel homeViewModel)
        {
            _homeViewModel = homeViewModel;
        }

        public DeviceViewModel() // 保留一個無參數建構函數，如果某些地方直接創建實例
        {
            // 如果直接創建，則 _homeViewModel 會是 null，需要注意 SaveNameCommand 的處理
            _homeViewModel = App.Host?.Services.GetService<HomeViewModel>()!; // 嘗試獲取，但這不是最佳實踐
        }


        [RelayCommand]
        private void EditName()
        {
            OriginalName = Name; // 保存當前名稱
            IsEditingName = true;
        }

        [RelayCommand]
        private async Task SaveNameAsync()
        {
            IsEditingName = false;
            // 呼叫 HomeViewModel 的方法來儲存到資料庫
            if (_homeViewModel != null)
            {
                await _homeViewModel.SaveChangesToDeviceAsync(this);
            }
            // 如果需要，可以在這裡添加一些錯誤處理或成功提示
        }

        [RelayCommand]
        private void CancelEditName()
        {
            Name = OriginalName; // 恢復原始名稱
            IsEditingName = false;
        }

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