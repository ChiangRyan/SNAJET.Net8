using CommunityToolkit.Mvvm.ComponentModel;
using SANJET.Core.Models;

namespace SANJET.Core.ViewModels
{
    // 這個類別專門用於在畫面上顯示紀錄，它比原始的 DeviceRecord 多了一個 RowNumber 屬性
    public partial class RecordDisplayItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _rowNumber; // 用於顯示排序

        [ObservableProperty]
        private DeviceRecord _record; // 原始的紀錄資料

        public RecordDisplayItemViewModel(int rowNumber, DeviceRecord record)
        {
            _rowNumber = rowNumber;
            _record = record;
        }
    }
}