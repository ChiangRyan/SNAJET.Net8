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
                    UniqueId = record.UniqueId, // 【新增】將 UniqueId 同步過去
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

        public async Task SyncRecordDeletionAsync(Guid recordUniqueId)
        {
            try
            {
                _logger.LogInformation("開始從 NAS 同步刪除紀錄 (UniqueId: {UniqueId})...", recordUniqueId);
                using var nasDbContext = CreateNasDbContext(); // 建立 NAS 資料庫的連線

                // 根據 UniqueId 找到 NAS 上的紀錄
                var recordToDeleteOnNas = await nasDbContext.DeviceRecords
                    .FirstOrDefaultAsync(r => r.UniqueId == recordUniqueId);

                if (recordToDeleteOnNas != null)
                {
                    nasDbContext.DeviceRecords.Remove(recordToDeleteOnNas);
                    await nasDbContext.SaveChangesAsync();
                    _logger.LogInformation("成功從 NAS 刪除紀錄 (UniqueId: {UniqueId})。", recordUniqueId);
                }
                else
                {
                    _logger.LogWarning("在 NAS 上找不到要刪除的紀錄 (UniqueId: {UniqueId})，可能已被刪除。", recordUniqueId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "從 NAS 同步刪除紀錄 (UniqueId: {UniqueId}) 時失敗。", recordUniqueId);
            }
        }

        public async Task SyncDeviceChangeAsync(Device device)
        {
            try
            {
                _logger.LogInformation("開始同步設備 (ID: {DeviceId}, Name: {DeviceName}) 的變更到 NAS...", device.Id, device.Name);
                using var nasDbContext = CreateNasDbContext();

                // 檢查 NAS 上是否已存在該設備
                var deviceOnNas = await nasDbContext.Devices.FirstOrDefaultAsync(d => d.Id == device.Id);

                if (deviceOnNas != null)
                {
                    // 如果存在，則更新屬性
                    _logger.LogInformation("在 NAS 上找到設備 ID: {DeviceId}，進行更新。", device.Id);
                    // 手動更新屬性，避免 EF Core 追蹤問題
                    deviceOnNas.Name = device.Name;
                    deviceOnNas.SlaveId = device.SlaveId;
                    deviceOnNas.Status = device.Status; // 也可以同步狀態
                    deviceOnNas.IsOperational = device.IsOperational;
                    deviceOnNas.RunCount = device.RunCount; // 也可以同步運轉次數
                    deviceOnNas.ControllingEsp32MqttId = device.ControllingEsp32MqttId;
                    deviceOnNas.Timestamp = DateTime.Now; // 更新時間戳
                }
                else
                {
                    // 如果不存在，則新增一筆
                    _logger.LogInformation("在 NAS 上找不到設備 ID: {DeviceId}，將其作為新設備新增。", device.Id);
                    // 關鍵：因為是單向同步，我們直接使用本地的 Id。
                    // 這需要確保 NAS 上的 Device 表主鍵不是自動遞增，或者允許身份插入。
                    // 對於 SQLite，這通常是可行的。
                    var newDeviceForNas = new Device
                    {
                        Id = device.Id, // 直接使用本地 ID
                        Name = device.Name,
                        SlaveId = device.SlaveId,
                        Status = device.Status,
                        IsOperational = device.IsOperational,
                        RunCount = device.RunCount,
                        ControllingEsp32MqttId = device.ControllingEsp32MqttId,
                        Timestamp = DateTime.Now
                    };
                    // 為了讓 EF Core 知道我們要插入一個帶有指定主鍵的新實體
                    nasDbContext.Add(newDeviceForNas).State = EntityState.Added;
                }

                await nasDbContext.SaveChangesAsync();
                _logger.LogInformation("成功將設備 (ID: {DeviceId}) 的變更同步到 NAS。", device.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步設備 (ID: {DeviceId}) 到 NAS 時失敗。", device.Id);
            }
        }

        public async Task SyncDeviceDeletionAsync(int deviceId)
        {
            try
            {
                _logger.LogInformation("開始從 NAS 同步刪除設備 (ID: {DeviceId})...", deviceId);
                using var nasDbContext = CreateNasDbContext();

                var deviceToDeleteOnNas = await nasDbContext.Devices.FindAsync(deviceId);

                if (deviceToDeleteOnNas != null)
                {
                    nasDbContext.Devices.Remove(deviceToDeleteOnNas);
                    await nasDbContext.SaveChangesAsync();
                    _logger.LogInformation("成功從 NAS 刪除設備 (ID: {DeviceId})。", deviceId);
                }
                else
                {
                    _logger.LogWarning("在 NAS 上找不到要刪除的設備 (ID: {DeviceId})，可能已被刪除。", deviceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "從 NAS 同步刪除設備 (ID: {DeviceId}) 時失敗。", deviceId);
            }
        }




    }
}