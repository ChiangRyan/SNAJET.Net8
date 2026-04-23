namespace SANJET.Core.Models
{
    public class CameraSettings
    {
        public string RtspUrl { get; set; } = string.Empty;
        public bool EnableHardwareDecoding { get; set; } = true;
        public int NetworkCachingMs { get; set; } = 300;
    }
}
