
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using System;

namespace SANJET.Core.Services
{
    /// <summary>
    /// 管理和通知 Modbus 輪詢服務的啟用狀態。
    /// </summary>
    public class PollingStateService : IPollingStateService
    {
        private readonly ILogger<PollingStateService> _logger;
        private bool _isPollingEnabled = false; // 預設為禁用

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
    }
}