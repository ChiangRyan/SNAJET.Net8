using System;

namespace SANJET.Core.Interfaces
{
    public interface IPollingStateService
    {
        bool IsPollingGloballyEnabled { get; }
        void SetPollingState(bool isEnabled);
        event EventHandler PollingStateChanged;
    }
}