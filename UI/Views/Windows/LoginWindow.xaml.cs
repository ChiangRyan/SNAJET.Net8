using SANJET.Core.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SANJET.UI.Views.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel; // 單一定義

        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel)); // 單一初始化
            DataContext = _viewModel;
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