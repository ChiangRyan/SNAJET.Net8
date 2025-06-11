// 建議新增檔案: Core/Interfaces/IDataSyncService.cs
using SANJET.Core.Models;
using System.Threading.Tasks;

namespace SANJET.Core.Interfaces
{
    public interface IDataSyncService
    {
        Task SyncRecordAdditionAsync(DeviceRecord record);
        Task SyncRecordDeletionAsync(int recordId);
        // 如果需要，可以添加更新的方法
        // Task SyncRecordUpdateAsync(DeviceRecord record);
    }
}