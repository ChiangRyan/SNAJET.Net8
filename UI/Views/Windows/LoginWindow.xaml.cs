using SANJET.Core.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SANJET.UI.Views.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            // 訂閱事件
            // 在 DataContext 設定後，如果 ViewModel 中有預設密碼，手動設定 PasswordBox
            // 假設您的 PasswordBox 在 XAML 中的 x:Name 是 "UserPasswordBox"

            if (!string.IsNullOrEmpty(_viewModel.Password))
            {
                PasswordBox.Password = _viewModel.Password; // PasswordBox 是 PasswordBox 的 x:Name
            }

            _viewModel.OnLoginSuccess += (s, e) => 
            {
                if (this.IsLoaded && this.IsActive) // 確保視窗是活動的
                {
                    DialogResult = true; // 設定 DialogResult 為 true，表示登入成功
                }
                Close(); };

            _viewModel.OnCancel += (s, e) => 
            {
                if (this.IsLoaded && this.IsActive) // Ensure the window is active
                {
                    DialogResult = false;
                }
                Close();
            };
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.Password = passwordBox.Password;
            }
        }
    }
}