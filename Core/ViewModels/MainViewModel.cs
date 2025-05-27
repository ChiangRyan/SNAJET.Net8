using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SANJET.Core.Constants.Enums;
using SANJET.Core.Interfaces;
using SANJET.UI.Views.Pages;
using SANJET.UI.Views.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace SANJET.Core.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;
        private readonly IMqttService _mqttService; // 注入 MQTT 服務
        private readonly ILogger<MainViewModel> _logger; // 修改為 MainViewModel 的 ILogger
        private Frame? _mainContentFrame;

        [ObservableProperty]
        private bool _isLedOn;

        [ObservableProperty]
        private string? _currentUser;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private bool _isHomeSelected;

        [ObservableProperty]
        private bool _canViewHome;

        [ObservableProperty]
        private bool _canControlDevice;

        [ObservableProperty]
        private bool _canAll;

        public MainViewModel(IAuthenticationService authService, IMqttService mqttService, ILogger<MainViewModel> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            UpdateLoginState(); // 建構時更新登入狀態
            IsHomeSelected = true; // 預設選中首頁
            _ = InitializeAsync(); // 初始化 MQTT 連線
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _mqttService.ConnectAsync();
                _logger.LogInformation("MQTT 連線成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT 連線失敗");
                MessageBox.Show("無法連接到 MQTT Broker，請檢查網路設置。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ToggleLed()
        {
            if (!CanControlDevice)
            {
                _logger.LogWarning("無權限控制設備，當前用戶: {CurrentUser}", CurrentUser);
                MessageBox.Show("您沒有權限控制設備！", "權限錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsLedOn = !IsLedOn;
                var payload = IsLedOn ? "ON" : "OFF";
                await _mqttService.PublishAsync("esp32/led/control", payload);
                _logger.LogInformation("LED 狀態已切換為 {State}", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送 MQTT 訊息失敗");
                MessageBox.Show("無法控制 LED，請檢查 MQTT 連線。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateLoginState()
        {
            var currentUserObject = _authService.GetCurrentUser();
            CurrentUser = currentUserObject?.Username;
            IsLoggedIn = currentUserObject != null;

            if (IsLoggedIn && currentUserObject != null && currentUserObject.PermissionsList != null)
            {
                CanViewHome = currentUserObject.PermissionsList.Contains(Permission.ViewHome.ToString()) ||
                              currentUserObject.PermissionsList.Contains(Permission.All.ToString());
                CanControlDevice = currentUserObject.PermissionsList.Contains(Permission.ControlDevice.ToString()) ||
                                   currentUserObject.PermissionsList.Contains(Permission.All.ToString());
                CanAll = currentUserObject.PermissionsList.Contains(Permission.All.ToString());
            }
            else
            {
                CanViewHome = false;
                CanControlDevice = false;
                CanAll = false;
                _mainContentFrame?.Navigate(null);
            }

            if (IsLoggedIn && IsHomeSelected && _mainContentFrame != null)
            {
                if (_mainContentFrame.Content == null || !(_mainContentFrame.Content is HomePage))
                {
                    _ = NavigateHomeAsync();
                }
            }
        }

        public void SetMainContentFrame(Frame frame)
        {
            _mainContentFrame = frame ?? throw new ArgumentNullException(nameof(frame));
            if (IsHomeSelected && _mainContentFrame.Content == null && IsLoggedIn)
            {
                _ = NavigateHomeAsync();
            }
        }

        [RelayCommand]
        private async Task NavigateHomeAsync()
        {
            if (_mainContentFrame != null)
            {
                IsHomeSelected = true;
                var homePage = new HomePage();

                if (App.Host != null)
                {
                    var homeViewModel = App.Host.Services.GetService<HomeViewModel>();
                    if (homeViewModel != null)
                    {
                        homeViewModel.CanControlDevice = CanControlDevice;
                        homePage.DataContext = homeViewModel;
                        await homeViewModel.LoadDevicesAsync();
                    }
                }

                _mainContentFrame.Navigate(homePage);
            }
        }

        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            UpdateLoginState();
        }

        [RelayCommand]
        private void ShowLogin()
        {
            if (App.Host != null)
            {
                var loginWindow = App.Host.Services.GetRequiredService<LoginWindow>();
                loginWindow.Owner = Application.Current.MainWindow;
                bool? result = loginWindow.ShowDialog();

                if (result == true)
                {
                    UpdateLoginState();
                }
            }
        }
    }
}