// 檔案路徑: UI/Views/Windows/MainWindow.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.ViewModels;
using System.Windows;
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
            _viewModel.SetMainContentFrame(MainContentFrame);
            DataContext = _viewModel;

            // 掛接 Loaded 事件，在視窗完全載入後觸發登入流程
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 應用程式首次啟動時，使用者尚未登入
                if (!_viewModel.IsLoggedIn)
                {
                    // 從 DI 容器獲取 LoginWindow 實例
                    var loginWindow = App.Host!.Services.GetRequiredService<LoginWindow>();

                    // 將 MainWindow 設置為 LoginWindow 的擁有者。
                    // Loaded 事件保證了 MainWindow 此時是可見且就緒的。
                    loginWindow.Owner = this;

                    // 以模態對話框的形式顯示登入視窗。
                    loginWindow.ShowDialog();

                    // 當登入視窗關閉後 (無論結果)，都呼叫 UpdateLoginState 來刷新介面和權限。
                    _viewModel.UpdateLoginState();
                }
            }
            catch (Exception ex)
            {
                // 如果在顯示登入視窗時發生錯誤，則顯示訊息並關閉程式
                MessageBox.Show($"顯示登入視窗時發生嚴重錯誤: {ex.Message}", "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            // 清除導覽紀錄，避免使用者可以點擊上一頁
            if (sender is System.Windows.Controls.Frame frame)
            {
                while (frame.CanGoBack)
                {
                    frame.RemoveBackEntry();
                }
            }
        }
    }
}