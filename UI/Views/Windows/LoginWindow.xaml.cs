using SANJET.Core.ViewModels;
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
            _viewModel.OnLoginSuccess += (s, e) => { DialogResult = true; Close(); };
            _viewModel.OnCancel += (s, e) => { DialogResult = false; Close(); };
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