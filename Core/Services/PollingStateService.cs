using SANJET.Core.Interfaces;
using System;

namespace SANJET.Core.Services
{
    public class PollingStateService : IPollingStateService
    {
        private bool _isPollingEnabled = false; // 預設為停用  

        public bool IsPollingGloballyEnabled => _isPollingEnabled;

        public event EventHandler PollingStateChanged = delegate { }; // 初始化為空的委派  

        public void SetPollingState(bool isEnabled)
        {
            if (_isPollingEnabled != isEnabled)
            {
                _isPollingEnabled = isEnabled;
                PollingStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}