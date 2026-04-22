
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace SANJET.Core.Services
{
    /// <summary>
    /// 管理和通知 Modbus 輪詢服務的啟用狀態。
    /// </summary>
    public class PollingStateService : IPollingStateService
    {
        private readonly ILogger<PollingStateService> _logger;
        private bool _isPollingEnabled = false; // 預設為禁用
        private bool _wasEnabledBeforePause = false; // 追蹤暫停前的狀態

        public bool IsPollingEnabled => _isPollingEnabled;
        public event Action? PollingStateChanged;

        public PollingStateService(ILogger<PollingStateService> logger)
        {
            _logger = logger;
        }

        public void EnablePolling()
        {
            if (!_isPollingEnabled)
            {
                _isPollingEnabled = true;
                _logger.LogInformation("輪詢狀態服務：輪詢已啟用。");
                PollingStateChanged?.Invoke();
            }
        }

        public void DisablePolling()
        {
            if (_isPollingEnabled)
            {
                _isPollingEnabled = false;
                _logger.LogInformation("輪詢狀態服務：輪詢已禁用。");
                PollingStateChanged?.Invoke();
            }
        }

        public async Task PausePollingAsync(int durationMilliseconds)
        {
            // 記錄暫停前的狀態
            _wasEnabledBeforePause = _isPollingEnabled;

            if (_isPollingEnabled)
            {
                DisablePolling();
                _logger.LogInformation("輪詢狀態服務：輪詢已暫停 {DurationMs} 毫秒（用於 Modbus 寫入操作）。", durationMilliseconds);
            }

            // 等待指定的時間
            await Task.Delay(durationMilliseconds);

            // 如果暫停前輪詢是啟用的，則恢復輪詢
            if (_wasEnabledBeforePause)
            {
                EnablePolling();
                _logger.LogInformation("輪詢狀態服務：暫停結束，輪詢已恢復。");
            }
        }
    }
}