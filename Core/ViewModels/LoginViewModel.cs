using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;
using SANJET.UI.Views.Windows;
using System.Windows;

namespace SANJET.Core.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;
        private readonly Window _window;

        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private string _password;

        [ObservableProperty]
        private string _errorMessage;

        public LoginViewModel(IAuthenticationService authService, Window window)
        {
            _authService = authService;
            _window = window;
            Username = string.Empty;
            Password = string.Empty;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "請輸入用戶名和密碼";
                return;
            }

            var user = await _authService.GetUserWithPermissionsAsync(Username, Password);
            if (user != null)
            {
                _window.DialogResult = true;
                _window.Close();

                var mainWindow = new MainWindow(App.Host.Services.GetRequiredService<MainViewModel>());
                mainWindow.Show();
            }
            else
            {
                ErrorMessage = "無效的用戶名或密碼";
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _window.DialogResult = false;
            _window.Close();
        }
    }
}