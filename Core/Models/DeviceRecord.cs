using System;
using System.ComponentModel.DataAnnotations;

namespace SANJET.Core.Models
{
    /// <summary>
    /// 代表一筆設備操作或測試的記錄。
    /// </summary>
    public class DeviceRecord
    {
        [Key]
        public int Id { get; set; }
        // 【新增】這是一個全域唯一的識別碼，用於跨資料庫同步

        public Guid UniqueId { get; set; }

        /// <summary>
        /// 關聯的設備 ID。
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 記錄當下的設備名稱。
        /// </summary>
        [Required]
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 記錄當下的設備運轉次數。
        /// </summary>
        public int RunCount { get; set; }

        /// <summary>
        /// 執行操作的使用者名稱。
        /// </summary>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 記錄的詳細內容。
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 記錄建立的時間戳。
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}