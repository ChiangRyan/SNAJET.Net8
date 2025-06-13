// 檔案路徑: Core/Services/DatabaseManagementService.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SANJET.Core.Services
{
    public class DatabaseManagementService : IDatabaseManagementService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<DatabaseManagementService> _logger;
        private readonly string _localDbPath;

        public DatabaseManagementService(AppDbContext dbContext, ILogger<DatabaseManagementService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;

            // 從 DbContext 的連線字串中解析出資料庫檔案的實際路徑
            var connectionString = _dbContext.Database.GetConnectionString();
            var builder = new SqliteConnectionStringBuilder(connectionString);
            _localDbPath = builder.DataSource;
            _logger.LogInformation("本地資料庫路徑解析為: {DbPath}", _localDbPath);
        }

        public async Task<bool> BackupDatabaseAsync(string destinationFilePath)
        {
            if (string.IsNullOrEmpty(_localDbPath) || !File.Exists(_localDbPath))
            {
                _logger.LogError("備份失敗：找不到來源資料庫檔案於 '{DbPath}'。", _localDbPath);
                return false;
            }

            try
            {
                // 使用 File.Copy 進行簡單的檔案複製備份
                // 這種 "冷備份" 方式在檔案被佔用時可能會失敗，但對於桌面應用是個簡單有效的方案
                await Task.Run(() => File.Copy(_localDbPath, destinationFilePath, true));
                _logger.LogInformation("資料庫已成功備份至: {DestinationPath}", destinationFilePath);
                return true;
            }
            catch (IOException ioEx)
            {
                _logger.LogError(ioEx, "備份失敗：資料庫檔案 '{DbPath}' 可能正在被使用中。", _localDbPath);
                MessageBox.Show($"備份失敗：無法讀取來源資料庫檔案。\n請稍後再試。\n\n錯誤詳情: {ioEx.Message}", "備份錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "備份資料庫時發生未預期的錯誤。");
                MessageBox.Show($"備份失敗：發生未預期的錯誤。\n\n錯誤詳情: {ex.Message}", "備份錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task<bool> RestoreDatabaseAsync(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
            {
                _logger.LogError("還原失敗：找不到指定的備份檔案 '{SourcePath}'。", sourceFilePath);
                MessageBox.Show($"還原失敗：找不到備份檔案於:\n{sourceFilePath}", "還原錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                // 為了覆蓋檔案，應用程式必須關閉。
                // 這裡的邏輯是先複製檔案，然後提示使用者並重啟。
                await Task.Run(() => File.Copy(sourceFilePath, _localDbPath, true));
                _logger.LogInformation("資料庫已成功從 '{SourcePath}' 還原。", sourceFilePath);

                MessageBox.Show("資料庫還原成功！\n應用程式即將重新啟動以載入新資料。", "還原成功", MessageBoxButton.OK, MessageBoxImage.Information);

                // 優雅地重啟應用程式
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var processPath = Environment.ProcessPath;
                    if (processPath != null)
                    {
                        Process.Start(new ProcessStartInfo(processPath));
                    }
                    Application.Current.Shutdown();
                });

                return true;
            }
            catch (IOException ioEx)
            {
                _logger.LogError(ioEx, "還原失敗：目標資料庫檔案 '{DbPath}' 無法被覆寫，可能正在使用中。", _localDbPath);
                MessageBox.Show($"還原失敗：無法覆寫現有資料庫檔案。\n請關閉程式後手動取代檔案，或重試。\n\n錯誤詳情: {ioEx.Message}", "還原錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "還原資料庫時發生未預期的錯誤。");
                MessageBox.Show($"還原失敗：發生未預期的錯誤。\n\n錯誤詳情: {ex.Message}", "還原錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}