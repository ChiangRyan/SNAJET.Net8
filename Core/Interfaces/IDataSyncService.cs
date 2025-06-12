
using SANJET.Core.Models;
using System.Threading.Tasks;

namespace SANJET.Core.Interfaces
{
    public interface IDataSyncService
    {
        Task SyncRecordAdditionAsync(DeviceRecord record);
        Task SyncRecordDeletionAsync(Guid recordUniqueId);
        // 如果需要，可以添加更新的方法
        // Task SyncRecordUpdateAsync(DeviceRecord record);

        // ===== 新增以下兩個方法 =====
        /// <summary>
        /// 同步設備的變更 (新增或更新) 到 NAS。
        /// </summary>
        /// <param name="device">發生變更的設備實體。</param>
        Task SyncDeviceChangeAsync(Device device);

        /// <summary>
        /// 從 NAS 同步刪除指定的設備。
        /// </summary>
        /// <param name="deviceId">要刪除的設備 ID。</param>
        Task SyncDeviceDeletionAsync(int deviceId);
    }
}