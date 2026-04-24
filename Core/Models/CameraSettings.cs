namespace SANJET.Core.Models
{
    /// <summary>
    /// 摄像头配置模型
    /// </summary>
    public class CameraSettings
    {
        public int CameraId { get; set; }
        public string? IpAddress { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int StreamType { get; set; } // 0 = Stream1 (高品质), 1 = Stream2 (低延迟)

        public CameraSettings()
        {
            StreamType = 0;
        }

        public CameraSettings(int cameraId)
        {
            CameraId = cameraId;
            StreamType = 0;
        }

        /// <summary>
        /// 构建完整的RTSP URL
        /// </summary>
        public string BuildRtspUrl()
        {
            if (string.IsNullOrWhiteSpace(IpAddress) || 
                string.IsNullOrWhiteSpace(Username) || 
                string.IsNullOrWhiteSpace(Password))
            {
                return string.Empty;
            }

            string stream = StreamType == 0 ? "stream1" : "stream2";
            return $"rtsp://{Username}:{Password}@{IpAddress}:554/{stream}";
        }

        /// <summary>
        /// 验证配置是否完整
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(IpAddress) &&
                   !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password);
        }
    }
}
