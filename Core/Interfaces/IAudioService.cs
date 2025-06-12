
using System.Threading.Tasks;

namespace SANJET.Core.Interfaces
{
    public interface IAudioService
    {
        /// <summary>
        /// 異步播放啟動聲音序列 (先語音後音樂)。
        /// </summary>
        Task PlayStartSoundAsync();

        /// <summary>
        /// 異步播放停止聲音序列 (先語音後音樂)。
        /// </summary>
        Task PlayStopSoundAsync();
    }
}