
using System.Windows;
using System.Windows.Controls;

namespace SANJET.UI.Views.Pages
{
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            // 如果需要 ViewModel，在這裡設置 DataContext
            // DataContext = App.Host?.Services.GetService<HomeViewModel>();
        }
    }
}