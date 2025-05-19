using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private readonly Frame _mainContentFrame;

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

        public MainViewModel(IAuthenticationService authService, Frame mainContentFrame)
        {
            _authService = authService;
            _mainContentFrame = mainContentFrame;
            CurrentUser = _authService.GetCurrentUser();
            IsLoggedIn = CurrentUser != null;
            CanViewHome = IsLoggedIn && (CurrentUser == "administrator" || CurrentUser == "admin" || CurrentUser == "user");
            CanAll = IsLoggedIn && CurrentUser == "administrator";
            IsHomeSelected = true; // 預設選中首頁
        }

        [RelayCommand]
        private void NavigateHome()
        {
            IsHomeSelected = true;
            _mainContentFrame.Navigate(new HomePage());
        }

        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            IsLoggedIn = false;
            CanViewHome = false;
            CanAll = false;
            CurrentUser = null;
            var loginWindow = new LoginWindow();
            loginWindow.Show();

            // 關閉 MainWindow
            Window.GetWindow((App.Current as App).MainWindow).Close();
        }
    }
}