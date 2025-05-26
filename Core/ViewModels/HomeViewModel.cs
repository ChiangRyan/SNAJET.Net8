
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; // 確保這個 using 被添加
using System.Collections.ObjectModel;


namespace SANJET.Core.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        public HomeViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            Devices = [];
            // LoadDevicesAsync(); // 建議在頁面激活或需要時調用
        }

        [ObservableProperty]
        private ObservableCollection<DeviceViewModel> devices = [];

        [ObservableProperty]
        private bool canControlDevice; // 這個屬性來自 MainViewModel，確保在使用前已正確設置

        public async Task LoadDevicesAsync()
        {
            Devices.Clear();
            var devicesFromDb = await _dbContext.Devices.ToListAsync();
            foreach (var device in devicesFromDb)
            {
                var deviceVm = new DeviceViewModel(this) // 傳遞 HomeViewModel 實例
                {
                    Id = device.Id,
                    Name = device.Name,
                    OriginalName = device.Name, // 初始化 OriginalName
                    IpAddress = device.IpAddress,
                    SlaveId = device.SlaveId,
                    Status = device.Status,
                    IsOperational = device.IsOperational,
                    RunCount = device.RunCount,
                    IsEditingName = false // 初始為非編輯模式
                };
                Devices.Add(deviceVm);
            }
        }

        // 實現儲存變更到特定設備的方法
        public async Task SaveChangesToDeviceAsync(DeviceViewModel deviceVm)
        {
            if (deviceVm == null) return;

            var deviceInDb = await _dbContext.Devices.FindAsync(deviceVm.Id);
            if (deviceInDb != null)
            {
                deviceInDb.Name = deviceVm.Name;
                // 如果有其他屬性也需要通過 DeviceViewModel 修改並保存，在此處更新
                // deviceInDb.IsOperational = deviceVm.IsOperational;
                _dbContext.Devices.Update(deviceInDb);
                await _dbContext.SaveChangesAsync();
                deviceVm.OriginalName = deviceVm.Name; // 更新成功後，同步 OriginalName
            }
            // 可以加入日誌記錄或錯誤處理
        }
    }

    public partial class DeviceViewModel : ObservableObject
    {
        private readonly HomeViewModel _homeViewModel; // 用於回呼儲存操作

        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string originalName = string.Empty; // 用於取消編輯

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

        // 無參數構造函數，如果 App.Host 為 null，則 _homeViewModel 可能為 null
        public DeviceViewModel()
        {
            _homeViewModel = App.Host?.Services.GetService<HomeViewModel>()!;
        }


        [RelayCommand]
        private void EditName()
        {
            OriginalName = Name; // 保存當前名稱以便取消
            IsEditingName = true;
        }

        [RelayCommand]
        private async Task SaveNameAsync() // 注意異步方法命名约定
        {
            IsEditingName = false; // 完成後退出編輯模式
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
            Status = "運行中";
        }

        [RelayCommand]
        private void Stop()
        {
            Status = "閒置";
        }

        [RelayCommand]
        private void Record()
        {
            // 記錄邏輯
        }
    }
}