// Core/Interfaces/IPollingStateService.cs
using System;

namespace SANJET.Core.Interfaces
{
    /// <summary>
    /// 定義輪詢狀態控制服務的介面。
    /// </summary>
    public interface IPollingStateService
    {
        /// <summary>
        /// 獲取當前輪詢是否已啟用。
        /// </summary>
        bool IsPollingEnabled { get; }

        /// <summary>
        /// 當輪詢狀態變更時觸發的事件。
        /// </summary>
        event Action? PollingStateChanged;

        /// <summary>
        /// 啟用輪詢。
        /// </summary>
        void EnablePolling();

        /// <summary>
        /// 禁用輪詢。
        /// </summary>
        void DisablePolling();
    }
}