using LibVLCSharp.Shared;
using SANJET.Core.Models;
using SANJET.Core.ViewModels;
using System;
using System.Windows;
using System.Windows.Media;

// 避免命名衝突
using LibVlcMedia = LibVLCSharp.Shared.Media;
using LibVlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace SANJET.UI.Views.Windows
{
    public partial class CameraStreamWindow : Window
    {
        private LibVLC? _libVlc;
        private LibVlcMediaPlayer? _mediaPlayer;
        private LibVlcMedia? _media;
        private int _cameraId = 1;
        private CameraSettings? _cameraSettings;

        public CameraStreamWindow()
        {
            InitializeComponent();
            Loaded += CameraStreamWindow_Loaded;
            Unloaded += CameraStreamWindow_Unloaded;
        }

        /// <summary>
        /// 设置摄像头ID并开始连接
        /// </summary>
        public void SetCameraId(int cameraId, SettingsPageViewModel settingsViewModel)
        {
            _cameraId = cameraId;
            CameraIdTextBlock.Text = $"攝影機 {cameraId}";

            // 从SettingsPageViewModel获取摄像头设置
            _cameraSettings = settingsViewModel.GetCameraSettings(cameraId);
        }

        private void CameraStreamWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_libVlc != null) return;

            try
            {
                LibVLCSharp.Shared.Core.Initialize();

                // ✅ 強制 TCP + 降延遲
                _libVlc = new LibVLC(
                    "--network-caching=200",
                    "--rtsp-tcp"
                );

                _mediaPlayer = new LibVlcMediaPlayer(_libVlc);

                // 綁定 UI
                CameraVideoView.MediaPlayer = _mediaPlayer;

                // ✅ 加事件（重要）
                _mediaPlayer.Playing += (s, ev) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        CameraStatusTextBlock.Text = "狀態：播放中";
                        StatusIndicator.Fill = Brushes.LimeGreen;
                    });
                };

                _mediaPlayer.Buffering += (s, ev) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        CameraStatusTextBlock.Text = $"緩衝中：{ev.Cache:0}%";
                        StatusIndicator.Fill = Brushes.Orange;
                    });
                };

                _mediaPlayer.EncounteredError += (s, ev) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        CameraStatusTextBlock.Text = "狀態：串流錯誤";
                        StatusIndicator.Fill = Brushes.Red;
                    });
                };

                CameraStatusTextBlock.Text = "狀態：播放器已就緒";
                StatusIndicator.Fill = Brushes.DarkGreen;

                // 如果已设置摄像头ID，立即尝试连接
                if (_cameraSettings != null && _cameraSettings.IsValid())
                {
                    ConnectToCamera();
                }
                else
                {
                    CameraStatusTextBlock.Text = "狀態：未設定攝影機";
                    StatusIndicator.Fill = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                CameraStatusTextBlock.Text = $"初始化錯誤：{ex.Message}";
                StatusIndicator.Fill = Brushes.Red;
            }
        }

        /// <summary>
        /// 连接到摄像头并开始播放
        /// </summary>
        private void ConnectToCamera()
        {
            if (_mediaPlayer == null || _libVlc == null || _cameraSettings == null)
            {
                CameraStatusTextBlock.Text = "播放器未初始化";
                StatusIndicator.Fill = Brushes.Red;
                return;
            }

            try
            {
                string rtspUrl = _cameraSettings.BuildRtspUrl();

                if (string.IsNullOrWhiteSpace(rtspUrl))
                {
                    CameraStatusTextBlock.Text = "攝影機設定不完整";
                    StatusIndicator.Fill = Brushes.Red;
                    return;
                }

                StopPlayback();

                _media = new LibVLCSharp.Shared.Media(_libVlc, new Uri(rtspUrl));
                _media.AddOption(":network-caching=200");

                _mediaPlayer.Play(_media);

                CameraStatusTextBlock.Text = $"連線中...";
                StatusIndicator.Fill = Brushes.Orange;
            }
            catch (Exception ex)
            {
                CameraStatusTextBlock.Text = $"錯誤：{ex.Message}";
                StatusIndicator.Fill = Brushes.Red;
            }
        }

        private void StopPlayback()
        {
            if (_mediaPlayer != null)
            {
                if (_mediaPlayer.IsPlaying)
                    _mediaPlayer.Stop();
            }

            // ✅ 釋放舊 Media
            _media?.Dispose();
            _media = null;
        }

        private void CameraStreamWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposePlayer();
        }

        private void DisposePlayer()
        {
            StopPlayback();

            if (CameraVideoView != null)
                CameraVideoView.MediaPlayer = null;

            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();

            _mediaPlayer = null;
            _libVlc = null;
        }
    }
}