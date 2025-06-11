// 建議新增檔案: Core/Services/DataSyncService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;
using System;
using System.Threading.Tasks;

namespace SANJET.Core.Services
{
    public class DataSyncService : IDataSyncService
    {
        private readonly ILogger<DataSyncService> _logger;
        private readonly DbContextOptions<AppDbContext> _nasDbContextOptions;

        public DataSyncService(IConfiguration configuration, ILogger<DataSyncService> logger)
        {
            _logger = logger;
            var nasConnectionString = configuration.GetConnectionString("NasConnection");

            if (string.IsNullOrEmpty(nasConnectionString))
            {
                _logger.LogError("NAS connection string 'NasConnection' is not configured.");
                throw new InvalidOperationException("NAS connection string is missing.");
            }

            // 為NAS資料庫建立獨立的DbContext選項
            _nasDbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(nasConnectionString)
                .Options;
        }

        private AppDbContext CreateNasDbContext() => new AppDbContext(_nasDbContextOptions);

        public async Task SyncRecordAdditionAsync(DeviceRecord record)
        {
            try
            {
                _logger.LogInformation("開始同步新增的紀錄 (ID: {LocalId}) 到NAS...", record.Id);
                using var nasDbContext = CreateNasDbContext();

                // 建立一個新的實體，因為 'record' 已經被本地 DbContext 追蹤
                var recordForNas = new DeviceRecord
                {
                    // 不複製主鍵 Id，讓NAS資料庫自動生成
                    DeviceId = record.DeviceId,
                    DeviceName = record.DeviceName,
                    RunCount = record.RunCount,
                    Username = record.Username,
                    Content = record.Content,
                    Timestamp = record.Timestamp
                };

                nasDbContext.DeviceRecords.Add(recordForNas);
                await nasDbContext.SaveChangesAsync();
                _logger.LogInformation("成功將紀錄同步到NAS。本地ID: {LocalId}, NAS新ID: {NasId}", record.Id, recordForNas.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步新增紀錄 (本地ID: {LocalId}) 到NAS時失敗。", record.Id);
                // 在此處可以加入錯誤處理邏輯，例如將失敗的任務存入佇列以便稍後重試
            }
        }

        public async Task SyncRecordDeletionAsync(int recordId)
        {
            _logger.LogWarning("刪除同步功能尚未實作。需要一個可靠的方法來對應本地和遠端的紀錄。");
            // 刪除操作比較複雜，因為本地和遠端的紀錄ID不同。
            // 一個可能的作法是新增一個 'Guid' 欄位作為兩邊共同的唯一識別碼。
            // 或是根據 'DeviceName', 'Timestamp', 'Content' 等組合來尋找要刪除的遠端紀錄。
            await Task.CompletedTask;
        }
    }
}