using SANJET.Core.ViewModels;
using System.Windows;

namespace SANJET.UI.Views.Windows
{
    /// <summary>
    /// Interaction logic for RecordWindow.xaml
    /// </summary>
    public partial class RecordWindow : Window
    {
        // ViewModel 將透過建構函式傳入
        public RecordWindow(RecordViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}