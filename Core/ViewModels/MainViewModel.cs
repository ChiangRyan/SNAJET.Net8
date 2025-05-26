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

        public void UpdateLoginState()
        {
            CurrentUser = _authService.GetCurrentUser();
            IsLoggedIn = !string.IsNullOrEmpty(CurrentUser); // <<-- 重新設定 IsLoggedIn
                                                             // 同步更新其他依賴於登入狀態的屬性
            CanViewHome = IsLoggedIn && (CurrentUser == "administrator" || CurrentUser == "admin" || CurrentUser == "user");
            CanAll = IsLoggedIn && CurrentUser == "administrator";

            // 登入成功後，如果首頁被選中且 Frame 內容為空，則導航到首頁
            if (IsLoggedIn && IsHomeSelected && _mainContentFrame != null && _mainContentFrame.Content == null)
            {
                NavigateHome();
            }
            // 或者其他登入成功後需要執行的 UI 更新邏輯
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
            // 並且 App.Host 是可用的
            //if (App.Host != null)
            //{
            //    var loginWindow = App.Host.Services.GetRequiredService<LoginWindow>();
            //    //loginWindow.Show();
            //}

        }

        [RelayCommand]
        private void ShowLogin()
        {
            // 確保 App.Host 不為 null 並且可以取得 LoginWindow
            if (App.Host != null)
            {
                var loginWindow = App.Host.Services.GetRequiredService<LoginWindow>();
                loginWindow.Owner = Application.Current.MainWindow; // 設定擁有者為當前主視窗
                bool? result = loginWindow.ShowDialog(); // 顯示登入視窗並等待結果

            }

        }


    }
}