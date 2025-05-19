using SANJET.Core.ViewModels;
using SANJET.UI.Views.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SANJET.UI.Views.Windows
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.SetMainContentFrame(MainContentFrame); // 傳遞 Frame 引用
            DataContext = _viewModel;
            Loaded += MainWindow_Loaded; // 在載入後設置初始頁面
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsHomeSelected)
            {
                //MainContentFrame.Navigate(new HomePage());
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            MainContentFrame.RemoveBackEntry();
        }
    }
}