using PropertyChanged;
using SJAPP.Core.Model;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using SJAPP.Core.Helpers;
using Microsoft.Win32;
using System.IO;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Data;

namespace SJAPP.Core.ViewModel
{
    [AddINotifyPropertyChangedInterface]
    public class RecordViewModel : ViewModelBase
    {
        private readonly SqliteDataService _dataService;
        private readonly int _deviceId;
        private readonly string _deviceName;
        private readonly string _currentUsername;
        private readonly int _runcount;

        // 記錄集合
        public ObservableCollection<DeviceRecord> DeviceRecords { get; set; }
        // 篩選後的記錄集合（用於 DataGrid 顯示）
        private ICollectionView _filteredDeviceRecords;
        public ICollectionView FilteredDeviceRecords
        {
            get => _filteredDeviceRecords;
            set
            {
                _filteredDeviceRecords = value;
                OnPropertyChanged(nameof(FilteredDeviceRecords));
            }
        }
        // 選中的記錄
        public DeviceRecord SelectedRecord { get; set; }
        // 記錄內容
        public string RecordContent { get; set; }
        // 篩選條件
        public string FilterUsername { get; set; }
        public DateTime? FilterStartDate { get; set; }

        // 命令
        public ICommand AddRecordCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand DeleteRecordCommand { get; private set; }
        public ICommand ExportToExcelCommand { get; private set; }
        public ICommand ApplyFilterCommand { get; private set; }
        public ICommand ResetFilterCommand { get; private set; }

        public RecordViewModel(List<DeviceRecord> records, int deviceId, string deviceName, string username, int runcount, SqliteDataService dataService)
        {
            _dataService = dataService;
            _deviceId = deviceId;
            _deviceName = deviceName;
            _currentUsername = username;
            _runcount = runcount;

            Debug.WriteLine($"RecordViewModel 初始化: DeviceId={_deviceId}, DeviceName={_deviceName}, Username={_currentUsername}");
            if (!_dataService.DeviceExists(_deviceId))
            {
                Debug.WriteLine($"無效的 DeviceId: {_deviceId}");
                MessageBox.Show($"設備 ID {_deviceId} 不存在於資料庫中，請選擇有效設備", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 初始化記錄集合
            DeviceRecords = new ObservableCollection<DeviceRecord>(records.OrderByDescending(r => r.Timestamp));

            // 初始化 CollectionView 進行排序和篩選
            FilteredDeviceRecords = CollectionViewSource.GetDefaultView(DeviceRecords);
            FilteredDeviceRecords.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
            FilteredDeviceRecords.Filter = FilterRecords;

            // 初始化命令
            AddRecordCommand = new RelayCommand(AddRecord, CanAddRecord);
            RefreshCommand = new RelayCommand(RefreshRecords);
            DeleteRecordCommand = new RelayCommand(DeleteRecord, CanDeleteRecord);
            ExportToExcelCommand = new RelayCommand(ExportToExcel, CanExportToExcel);
            ApplyFilterCommand = new RelayCommand(ApplyFilter);
            ResetFilterCommand = new RelayCommand(ResetFilter);
        }

        private bool CanAddRecord()
        {
            return !string.IsNullOrWhiteSpace(RecordContent);
        }

        private bool CanDeleteRecord()
        {
            return SelectedRecord != null;
        }

        private bool CanExportToExcel()
        {
            return DeviceRecords != null && DeviceRecords.Count > 0;
        }

        private void AddRecord()
        {
            try
            {
                Debug.WriteLine(
                   $" AddRecord: DeviceId={_deviceId}," +
                   $" DeviceName={_deviceName}," +
                   $" Username={_currentUsername}," +
                   $" Runcount={_runcount}," +
                   $" Content={RecordContent}"
                );

                // 建立新記錄
                var newRecord = new DeviceRecord
                {
                    DeviceId = _deviceId,
                    DeviceName = _deviceName,
                    RunCount = _runcount,
                    Username = _currentUsername,
                    Content = RecordContent.Trim(),
                    Timestamp = DateTime.Now
                };

                // 保存到資料庫
                _dataService.AddDeviceRecord(newRecord);

                // 刷新顯示
                RefreshRecords();

                // 清空輸入
                RecordContent = string.Empty;

                MessageBox.Show("記錄已成功添加！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AddRecord failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"添加記錄時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteRecord()
        {
            try
            {
                if (SelectedRecord == null)
                {
                    MessageBox.Show("請先選擇要刪除的記錄", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 確認刪除
                var result = MessageBox.Show($"確定要刪除ID為 {SelectedRecord.Id} 的記錄嗎？", "確認刪除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Debug.WriteLine($"正在刪除記錄 ID: {SelectedRecord.Id}，設備 ID: {_deviceId}");

                    // 從資料庫中刪除，傳遞 deviceId 和 recordId
                    bool success = _dataService.DeleteDeviceRecord(_deviceId, SelectedRecord.Id);

                    if (success)
                    {
                        // 從集合中移除
                        DeviceRecords.Remove(SelectedRecord);
                        MessageBox.Show("記錄已成功刪除！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("刪除記錄失敗！記錄可能已被其他用戶刪除。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        // 刷新所有記錄
                        RefreshRecords();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DeleteRecord failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"刪除記錄時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshRecords()
        {
            try
            {
                // 從資料庫中重新讀取此設備的記錄
                var records = _dataService.GetDeviceRecords(_deviceId);

                // 更新記錄集合
                DeviceRecords.Clear();
                foreach (var record in records.OrderByDescending(r => r.Timestamp))
                {
                    DeviceRecords.Add(record);
                }
                // 重新應用篩選
                FilteredDeviceRecords.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新記錄時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel()
        {
            try
            {
                // 建立儲存檔案對話框
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel檔案 (*.xlsx)|*.xlsx",
                    Title = "導出設備記錄",
                    FileName = $"{_deviceName}_記錄_{DateTime.Now:yyyyMMdd}",
                    DefaultExt = ".xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;
                    // 檢查檔案是否已存在
                    string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "導出模板", "CP08-003-02版-產品測試記錄表.xlsx");
                    // 檢查模板檔案是否存在
                    if (!File.Exists(templatePath))
                    {
                        MessageBox.Show($"模板檔案不存在: {templatePath}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    // 設定EPPlus許可模式
                    // 如果你代表「機構」以非商業方式使用：
                    ExcelPackage.License.SetNonCommercialOrganization("Sanjet");
                    // 或者，若只是「個人」非商業使用：
                    //ExcelPackage.License.SetNonCommercialPersonal("你的姓名");

                    // 使用模板載入
                    using (var package = new ExcelPackage(new FileInfo(templatePath)))
                    {
                        var worksheet = package.Workbook.Worksheets[0]; // 假設只用第一個工作表

                        // 創建工作表
                        //var worksheet = package.Workbook.Worksheets.Add($"{_deviceName}記錄");

                        int startRow = 4; // 根據模板格式調整填寫起始列

                        foreach (var record in DeviceRecords.OrderBy(r => r.Timestamp))  // 根據時間排序「舊 → 新」
                        {
                            worksheet.Cells[startRow, 1].Value = record.Id;
                            worksheet.Cells[startRow, 2].Value = record.Timestamp;
                            worksheet.Cells[startRow, 2].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                            worksheet.Cells[startRow, 3].Value = record.DeviceName;
                            worksheet.Cells[startRow, 4].Value = record.RunCount;
                            worksheet.Cells[startRow, 5].Value = record.Content;
                            worksheet.Cells[startRow, 6].Value = record.Username;
                            startRow++;

                            // 自動調整欄寬
                            worksheet.Cells[1, 1, startRow - 1, 6].AutoFitColumns();
                        }

                        /*
                        // 設定標題行
                        worksheet.Cells[1, 1].Value = "排序";
                        worksheet.Cells[1, 2].Value = "日期時間";
                        worksheet.Cells[1, 3].Value = "機種";
                        worksheet.Cells[1, 4].Value = "跑合";
                        worksheet.Cells[1, 5].Value = "測試狀況";
                        worksheet.Cells[1, 6].Value = "使用者";

                        // 格式化標題行
                        using (var range = worksheet.Cells[1, 1, 1, 6])
                        {
                            range.Style.Font.Bold = true;
                            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            range.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                            range.Style.Font.Size = 12;
                            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        }

                        // 填充資料
                        int row = 2;
                        foreach (var record in DeviceRecords)
                        {
                            worksheet.Cells[row, 1].Value = record.Id;
                            worksheet.Cells[row, 2].Value = record.Timestamp;
                            worksheet.Cells[row, 2].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                            worksheet.Cells[row, 3].Value = record.DeviceName;
                            worksheet.Cells[row, 4].Value = record.RunCount;
                            worksheet.Cells[row, 5].Value = record.Content;
                            worksheet.Cells[row, 6].Value = record.Username;
                            row++;
                        }

                        // 自動調整欄寬
                        worksheet.Cells[1, 1, row - 1, 6].AutoFitColumns();

                        // 設定備註信息
                        //worksheet.Cells[row + 1, 1].Value = $"導出時間: {DateTime.Now}";
                        //worksheet.Cells[row + 2, 1].Value = $"導出者: {_currentUsername}";

                         */

                        // 保存檔案
                        package.SaveAs(new FileInfo(filePath));
                    }

                    MessageBox.Show($"記錄已成功導出至: {filePath}", "導出成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 詢問用戶是否打開檔案
                    if (MessageBox.Show("是否立即打開導出的檔案？", "操作確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExportToExcel failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"導出Excel時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool FilterRecords(object item)
        {
            var record = item as DeviceRecord;
            if (record == null) return false;

            bool matches = true;

            // 篩選使用者
            if (!string.IsNullOrWhiteSpace(FilterUsername))
            {
                matches &= record.Username.IndexOf(FilterUsername, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // 篩選開始日期
            if (FilterStartDate.HasValue)
            {
                matches &= record.Timestamp.Date >= FilterStartDate.Value.Date;
            }

            return matches;
        }

        private void ApplyFilter()
        {
            try
            {
                Debug.WriteLine("Applying filter...");
                FilteredDeviceRecords.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyFilter failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"應用篩選時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetFilter()
        {
            try
            {
                Debug.WriteLine("Resetting filter...");
                 FilterUsername = string.Empty;
                FilterStartDate = null;
                FilteredDeviceRecords.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResetFilter failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"重置篩選時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

