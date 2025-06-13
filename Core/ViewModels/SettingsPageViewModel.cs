
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SANJET.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace SANJET.Core.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly ILogger<SettingsPageViewModel> _logger;
        private readonly IDatabaseManagementService _dbManagementService;

        [ObservableProperty]
        private string _pageTitle = "應用程式設定";

        public SettingsPageViewModel(ILogger<SettingsPageViewModel> logger, IDatabaseManagementService dbManagementService)
        {
            _logger = logger;
            _dbManagementService = dbManagementService;
            _logger.LogInformation("SettingsViewModel 已初始化。");
        }

        public void LoadSettings()
        {
            _logger.LogInformation("正在加載設定值...");
        }

        [RelayCommand]
        private async Task BackupDatabaseAsync()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "資料庫備份檔案 (*.db)|*.db|所有檔案 (*.*)|*.*",
                Title = "選擇備份路徑",
                FileName = $"SNAJET_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string destinationPath = saveFileDialog.FileName;
                _logger.LogInformation("使用者選擇備份路徑: {Path}", destinationPath);
                bool success = await _dbManagementService.BackupDatabaseAsync(destinationPath);
                if (success)
                {
                    MessageBox.Show($"資料庫已成功備份至:\n{destinationPath}", "備份成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                // 失敗的訊息由服務層顯示
            }
        }

        [RelayCommand]
        private async Task RestoreDatabaseAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "資料庫備份檔案 (*.db)|*.db|所有檔案 (*.*)|*.*",
                Title = "選擇要還原的備份檔案"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string sourcePath = openFileDialog.FileName;
                var result = MessageBox.Show(
                    "警告：此操作將會用選擇的備份檔案覆蓋目前的資料庫。\n\n所有未備份的變更都將遺失，且應用程式將會重新啟動。\n\n確定要繼續嗎？",
                    "確認還原",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _logger.LogInformation("使用者確認從 '{Path}' 還原資料庫。", sourcePath);
                    await _dbManagementService.RestoreDatabaseAsync(sourcePath);
                    // 成功還原後，服務會處理重啟邏輯，此處不需再做操作
                }
            }
        }
    }
}