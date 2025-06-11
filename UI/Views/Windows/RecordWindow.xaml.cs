using SANJET.Core.ViewModels;
using System.Windows;
using System.Windows.Controls; // 確保引用此命名空間

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

        // 【新增這個方法】
        // 當 DataGrid 的每一行在載入時，這個事件會被觸發
        private void RecordsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 設定每一行最前面的標頭 (RowHeader) 為它的索引值+1
            // 這樣就會顯示 1, 2, 3...
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }
    }
}