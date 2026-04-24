using LibVLCSharp.Shared;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibVlcMedia = LibVLCSharp.Shared.Media;
using LibVlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace SANJET.UI.Views.Pages
{
    public partial class SettingsPage : Page
    {
        private LibVLC? _libVlc;
        private LibVlcMediaPlayer? _mediaPlayer;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            Unloaded += SettingsPage_Unloaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_libVlc != null)
            {
                return;
            }

            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC("--network-caching=300", "--rtsp-tcp");
            _mediaPlayer = new LibVlcMediaPlayer(_libVlc);
            RtspVideoView.MediaPlayer = _mediaPlayer;

            RtspStatusTextBlock.Text = "狀態：播放器已就緒";
            RtspStatusTextBlock.Foreground = Brushes.DarkGreen;
        }

        private void StartRtspButton_Click(object sender, RoutedEventArgs e)
        {
            var mediaPlayer = _mediaPlayer;
            var libVlc = _libVlc;
            if (mediaPlayer is null || libVlc is null)
            {
                RtspStatusTextBlock.Text = "狀態：播放器初始化失敗";
                RtspStatusTextBlock.Foreground = Brushes.Red;
                return;
            }

            string rtspUrl = RtspUrlTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rtspUrl))
            {
                RtspStatusTextBlock.Text = "狀態：請輸入 RTSP 位址";
                RtspStatusTextBlock.Foreground = Brushes.OrangeRed;
                return;
            }

            try
            {
                using var media = new LibVlcMedia(libVlc, new Uri(rtspUrl));
                mediaPlayer.Play(media);
                RtspStatusTextBlock.Text = "狀態：連線中...";
                RtspStatusTextBlock.Foreground = Brushes.DodgerBlue;
            }
            catch (Exception ex)
            {
                RtspStatusTextBlock.Text = $"狀態：連線失敗 - {ex.Message}";
                RtspStatusTextBlock.Foreground = Brushes.Red;
            }
        }

        private void StopRtspButton_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            RtspStatusTextBlock.Text = "狀態：已停止";
            RtspStatusTextBlock.Foreground = Brushes.Gray;
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposePlayer();
        }

        private void StopPlayback()
        {
            if (_mediaPlayer?.IsPlaying == true)
            {
                _mediaPlayer.Stop();
            }
        }

        private void DisposePlayer()
        {
            StopPlayback();
            RtspVideoView.MediaPlayer = null;
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
            _mediaPlayer = null;
            _libVlc = null;
        }
    }
}
