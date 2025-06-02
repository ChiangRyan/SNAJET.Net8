// 檔案路徑: SANJET.Core/ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace SANJET.Core.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly ILogger<SettingsPageViewModel> _logger;

        [ObservableProperty]
        private string _pageTitle = "應用程式設定"; // 範例屬性，用於頁面標題

        // 在此可以添加更多設定相關的屬性和命令
        // 例如：
        // [ObservableProperty]
        // private string _someSettingValue;

        // [RelayCommand]
        // private async Task SaveSomeSettingAsync() { /* ... */ }

        public SettingsPageViewModel(ILogger<SettingsPageViewModel> logger)
        {
            _logger = logger;
            _logger.LogInformation("SettingsViewModel 已初始化。"); // SettingsViewModel initialized.
            // 可以調用一個方法來加載初始設定
            LoadSettings();
        }

        public void LoadSettings()
        {
            _logger.LogInformation("正在加載設定值..."); // Loading setting values...
            // 此處應實現從持久化存儲（例如設定檔案、資料庫）加載設定的邏輯
            // 例如: SomeSettingValue = _settingsService.GetSomeSetting();
        }

        // 如果有需要保存設定的動作，可以添加類似方法
        public void SaveSettings()
        {
            _logger.LogInformation("正在保存設定值..."); // Saving setting values...
            // 此處應實現保存設定到持久化存儲的邏輯
        }
    }
}