using LibVLCSharp.Shared;
using Microsoft.Extensions.Options;
using SANJET.Core.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SANJET.UI.Views.Pages
{
    public partial class HomePage : Page
    {
        private LibVLC? _libVlc;
        private MediaPlayer? _mediaPlayer;
        private readonly CameraSettings _cameraSettings;

        public HomePage()
        {
            InitializeComponent();

            _cameraSettings = App.Host?.Services.GetService(typeof(IOptions<CameraSettings>)) is IOptions<CameraSettings> options
                ? options.Value
                : new CameraSettings();

            RtspUrlTextBox.Text = _cameraSettings.RtspUrl;
            Loaded += HomePage_Loaded;
            Unloaded += HomePage_Unloaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_libVlc != null)
            {
                return;
            }

            Core.Initialize();

            _libVlc = _cameraSettings.EnableHardwareDecoding
                ? new LibVLC("--avcodec-hw=d3d11va")
                : new LibVLC();

            _mediaPlayer = new MediaPlayer(_libVlc);
            CameraVideoView.MediaPlayer = _mediaPlayer;
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAndReleasePlayer();
        }

        private void ConnectCameraButton_Click(object sender, RoutedEventArgs e)
        {
            var url = RtspUrlTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("請先輸入 RTSP URL。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_libVlc == null || _mediaPlayer == null)
            {
                HomePage_Loaded(this, e);
            }

            if (_libVlc == null || _mediaPlayer == null)
            {
                MessageBox.Show("播放器初始化失敗。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using var media = new Media(_libVlc, new Uri(url));
            media.AddOption($":network-caching={Math.Max(100, _cameraSettings.NetworkCachingMs)}");
            media.AddOption(":rtsp-tcp");

            var playResult = _mediaPlayer.Play(media);
            CameraStatusTextBlock.Text = playResult ? "連線中..." : "連線失敗";
        }

        private void StopCameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer?.IsPlaying == true)
            {
                _mediaPlayer.Stop();
            }

            CameraStatusTextBlock.Text = "已停止";
        }

        private void StopAndReleasePlayer()
        {
            if (_mediaPlayer != null)
            {
                if (_mediaPlayer.IsPlaying)
                {
                    _mediaPlayer.Stop();
                }

                CameraVideoView.MediaPlayer = null;
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            _libVlc?.Dispose();
            _libVlc = null;
        }
    }
}
