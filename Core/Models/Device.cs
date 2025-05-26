
namespace SANJET.Core.Models
{
    public class Device
    {
        public int Id { get; set; } // 主鍵
        public string Name { get; set; } = string.Empty;
        //public string OriginalName { get; set; } = string.Empty; // 用於儲存原始名稱
        public string IpAddress { get; set; } = string.Empty;
        public int SlaveId { get; set; }
        public string Status { get; set; } = "閒置"; // 預設狀態
        public bool IsOperational { get; set; } = true; // 預設為可操作
        public int RunCount { get; set; } = 0; // 預設運轉次數
        public DateTime Timestamp { get; set; }



        // 如果需要，可以添加其他與資料庫儲存相關的屬性
        // 例如：
        // public DateTime LastCommunicationTime { get; set; }
        // public string? Location { get; set; }
    }
}