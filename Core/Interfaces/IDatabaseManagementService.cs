// 檔案路徑: Core/Interfaces/IDatabaseManagementService.cs
using System.Threading.Tasks;

namespace SANJET.Core.Interfaces
{
    /// <summary>
    /// 提供資料庫備份與還原功能的服務介面。
    /// </summary>
    public interface IDatabaseManagementService
    {
        /// <summary>
        /// 將當前應用的本地資料庫備份到指定路徑。
        /// </summary>
        /// <param name="destinationFilePath">備份檔案的完整儲存路徑。</param>
        /// <returns>表示操作成功或失敗的布林值。</returns>
        Task<bool> BackupDatabaseAsync(string destinationFilePath);

        /// <summary>
        /// 從指定的備份檔案還原資料庫。
        /// 此操作會覆蓋現有資料庫，並觸發應用程式重啟。
        /// </summary>
        /// <param name="sourceFilePath">備份檔案的完整來源路徑。</param>
        /// <returns>表示操作成功或失敗的布林值。</returns>
        Task<bool> RestoreDatabaseAsync(string sourceFilePath);
    }
}