using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.Interfaces;
using SANJET.UI.Views.Pages;
using SANJET.UI.Views.Windows;
using System.Windows;
using System.Windows.Controls;

namespace SANJET.Core.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;
        private Frame? _mainContentFrame;

        public MainViewModel(IAuthenticationService authService)
        {
            _authService = authService;
            CurrentUser = _authService.GetCurrentUser(); // 確保 GetCurrentUser() 的邏輯正確
            IsLoggedIn = !string.IsNullOrEmpty(CurrentUser); // 基於 CurrentUser 是否為 null 或空來判斷
            CanViewHome = IsLoggedIn && (CurrentUser == "administrator" || CurrentUser == "admin" || CurrentUser == "user");
            CanAll = IsLoggedIn && CurrentUser == "administrator";
            IsHomeSelected = true; // 預設選中首頁

            // 注意：如果 CurrentUser 為 null，後續依賴 CurrentUser 的邏輯需要處理 null 情況
            // 例如 _authService.GetCurrentUser() 返回 null 時，IsLoggedIn 等屬性的判斷。
        }

        // 加入 SetMainContentFrame 方法
        public void SetMainContentFrame(Frame frame)
        {
            _mainContentFrame = frame ?? throw new ArgumentNullException(nameof(frame));
            // 如果需要在設定 Frame 後立即導航到首頁，可以在這裡處理
            // 例如，如果 IsHomeSelected 為 true 且 Frame 內容為空
            if (IsHomeSelected && _mainContentFrame.Content == null)
            {
                NavigateHome(); // 確保 NavigateHome 使用 _mainContentFrame
            }
        }

        [ObservableProperty]
        private string? _currentUser;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private bool _isHomeSelected;

        [ObservableProperty]
        private bool _canViewHome;

        [ObservableProperty]
        private bool _canAll;

        [RelayCommand]
        private void NavigateHome()
        {
            if (_mainContentFrame != null) // 增加 null 檢查
            {
                IsHomeSelected = true;
                _mainContentFrame.Navigate(new HomePage());
            }
        }


        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            IsLoggedIn = false;
            CanViewHome = false;
            CanAll = false;
            CurrentUser = null;

            // 顯示登入視窗
            // 假設 LoginWindow 是通過 DI 獲取的
            // 並且 App.Host 是可用的
            if (App.Host != null)
            {
                var loginWindow = App.Host.Services.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }


            // 關閉 MainWindow
            // Application.Current.MainWindow 是 WPF 的一個屬性，它會被設為第一個 Show() 的 Window
            // 或者可以通過 Application.MainWindow = someWindow; 明確設定
            // 如果 MainWindow 是當前 Application.Current.MainWindow
            var currentMainWindow = Application.Current.MainWindow;
            if (currentMainWindow != null && currentMainWindow is MainWindow) // 確認它是 MainWindow 類型，避免關錯
            {
                currentMainWindow.Close();
            }

        }
    }
}