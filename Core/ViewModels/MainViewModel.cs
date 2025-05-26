using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.Constants.Enums; // <-- 引入 Permission Enum
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
            UpdateLoginState(); // 建構時即更新一次狀態
            IsHomeSelected = true; // 預設選中首頁
        }

        public void UpdateLoginState()
        {
            var currentUserObject = _authService.GetCurrentUser();
            CurrentUser = currentUserObject?.Username; // 用於顯示
            IsLoggedIn = currentUserObject != null;

            if (IsLoggedIn && currentUserObject != null && currentUserObject.PermissionsList != null)
            {
                // 使用 PermissionsList 和 Permission Enum 進行判斷
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
            }

            if (IsLoggedIn && IsHomeSelected && _mainContentFrame != null && _mainContentFrame.Content == null)
            {
                NavigateHome();
            }
        }

        public void SetMainContentFrame(Frame frame)
        {
            _mainContentFrame = frame ?? throw new ArgumentNullException(nameof(frame));
            if (IsHomeSelected && _mainContentFrame.Content == null && IsLoggedIn) // 確保登入後才導航
            {
                NavigateHome();
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
        private bool _canControlDevice; // 新增此屬性，用於控制 "ControlDevice" 權限

        [ObservableProperty]
        private bool _canAll;


        [RelayCommand]
        private void NavigateHome()
        {
            if (_mainContentFrame != null)
            {
                IsHomeSelected = true;
                var homePage = new HomePage();

                // 如果需要設置 ViewModel
                if (App.Host != null)
                {
                    var homeViewModel = App.Host.Services.GetService<HomeViewModel>();
                    if (homeViewModel != null)
                    {
                        // 更新權限狀態
                        //homeViewModel.CanControlDevice = CanControlDevice;
                        homePage.DataContext = homeViewModel;
                    }
                }

                _mainContentFrame.Navigate(homePage);
            }
        }


        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            UpdateLoginState(); // 呼叫 UpdateLoginState 來重置所有權限相關狀態

            // 顯示登入視窗的邏輯 (可選，取決於您的流程)
            // if (App.Host != null)
            // {
            //     ShowLogin(); // 可以直接呼叫 ShowLogin Command
            // }
        }

        [RelayCommand]
        private void ShowLogin()
        {
            if (App.Host != null)
            {
                var loginWindow = App.Host.Services.GetRequiredService<LoginWindow>();
                loginWindow.Owner = Application.Current.MainWindow;
                bool? result = loginWindow.ShowDialog();

                if (result == true) // 登入成功
                {
                    UpdateLoginState(); // 登入成功後，再次更新主界面的狀態
                }
                // else: 登入失敗或取消，LoginWindow 會自行關閉
            }
        }
    }
}