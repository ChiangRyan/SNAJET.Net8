using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SANJET.UI.Views.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = App.Host.Services.GetRequiredService<LoginViewModel>();
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