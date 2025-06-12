// 檔案路徑: Core/Services/AudioService.cs
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;
using System;
using System.IO;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SANJET.Core.Services
{
    public class AudioService : IAudioService
    {
        private readonly ILogger<AudioService> _logger;
        private readonly SpeechSynthesizer _speechSynthesizer;
        private readonly MediaPlayer _mediaPlayer;
        private bool _isPlaying = false;

        // --- 音訊檔案路徑設定 ---
        // Path.Combine 確保在不同作業系統上路徑格式都正確
        // AppDomain.CurrentDomain.BaseDirectory 會取得程式執行的目錄 (例如 bin/Debug/net8.0-windows)
        private static readonly string StartMusicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "start_music.mp3");
        private static readonly string StopMusicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "stop_music.mp3");

        public AudioService(ILogger<AudioService> logger)
        {
            _logger = logger;
            _speechSynthesizer = new SpeechSynthesizer();
            // 設定語音輸出的裝置，通常是預設
            _speechSynthesizer.SetOutputToDefaultAudioDevice();

            _mediaPlayer = new MediaPlayer();
            // 當音樂播放完畢時，重設 _isPlaying 旗標
            _mediaPlayer.MediaEnded += (s, e) =>
            {
                _isPlaying = false;
                _logger.LogInformation("音樂播放完畢。");
            };
        }

        public async Task PlayStartSoundAsync()
        {
            await PlaySequenceAsync("設備自動啟動中，請注意安全", StartMusicPath);
        }

        public async Task PlayStopSoundAsync()
        {
            await PlaySequenceAsync("停止程序開始", StopMusicPath);
        }

        private async Task PlaySequenceAsync(string textToSpeak, string musicFilePath)
        {
            // 如果正在播放，則不執行新的播放請求，避免聲音重疊
            if (_isPlaying)
            {
                _logger.LogWarning("正在播放音訊，已忽略新的播放請求。");
                return;
            }

            try
            {
                _isPlaying = true;

                // 1. 播報文字
                _logger.LogInformation("正在播報文字: '{Text}'", textToSpeak);
                // 使用 Task.Run 在背景執行緒上播放語音，避免凍結 UI
                await Task.Run(() => _speechSynthesizer.Speak(textToSpeak));
                _logger.LogInformation("文字播報完畢。");

                // 2. 播放音樂
                if (File.Exists(musicFilePath))
                {
                    _logger.LogInformation("正在播放音樂: {FilePath}", musicFilePath);
                    _mediaPlayer.Open(new Uri(musicFilePath));
                    _mediaPlayer.Play();
                }
                else
                {
                    _logger.LogError("找不到指定的音樂檔案: {FilePath}", musicFilePath);
                    // 如果檔案不存在，也應該重設旗標
                    _isPlaying = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "播放音訊序列時發生錯誤。");
                _isPlaying = false; // 發生錯誤時重設旗標
            }
        }
    }
}