using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using SANJET.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SANJET.Core.ViewModels
{
    public partial class RecordViewModel : ObservableObject
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<RecordViewModel> _logger;
        private readonly DeviceViewModel _deviceViewModel; // 關聯的設備 ViewModel
        private readonly string _currentUsername;

        [ObservableProperty]
        private string _recordContent = string.Empty;

        [ObservableProperty]
        private DeviceRecord? _selectedRecord;

        [ObservableProperty]
        private string? _filterUsername;

        [ObservableProperty]
        private DateTime? _filterStartDate;

        public ObservableCollection<DeviceRecord> DeviceRecords { get; } = new();
        public ICollectionView FilteredDeviceRecords { get; }

        // ViewModel 的建構函式，接收必要的依賴和資料
        public RecordViewModel(DeviceViewModel deviceViewModel, AppDbContext dbContext, ILogger<RecordViewModel> logger, string currentUsername)
        {
            _deviceViewModel = deviceViewModel;
            _dbContext = dbContext;
            _logger = logger;
            _currentUsername = currentUsername;

            // 初始化 CollectionView 用於篩選和排序
            FilteredDeviceRecords = CollectionViewSource.GetDefaultView(DeviceRecords);
            FilteredDeviceRecords.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
            FilteredDeviceRecords.Filter = FilterRecords;

            // 非同步加載初始資料
            _ = LoadRecordsAsync();
        }

        private async Task LoadRecordsAsync()
        {
            try
            {
                _logger.LogInformation("正在為設備 ID: {DeviceId} 加載紀錄...", _deviceViewModel.Id);
                DeviceRecords.Clear();
                var records = await _dbContext.DeviceRecords
                    .Where(r => r.DeviceId == _deviceViewModel.Id)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync();

                foreach (var record in records)
                {
                    DeviceRecords.Add(record);
                }
                _logger.LogInformation("成功為設備 ID: {DeviceId} 加載了 {Count} 筆紀錄。", _deviceViewModel.Id, DeviceRecords.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加載設備紀錄時發生錯誤。");
                MessageBox.Show($"加載紀錄失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddOrDeleteRecord))]
        private async Task AddRecordAsync()
        {
            if (string.IsNullOrWhiteSpace(RecordContent))
            {
                MessageBox.Show("記錄內容不能為空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newRecord = new DeviceRecord
                {
                    DeviceId = _deviceViewModel.Id,
                    DeviceName = _deviceViewModel.Name, // 使用 ViewModel 的當前名稱
                    RunCount = _deviceViewModel.RunCount, // 使用 ViewModel 的當前運轉次數
                    Username = _currentUsername,
                    Content = RecordContent.Trim(),
                    Timestamp = DateTime.Now
                };

                _dbContext.DeviceRecords.Add(newRecord);
                await _dbContext.SaveChangesAsync();

                DeviceRecords.Insert(0, newRecord); // 新紀錄加到最前面
                RecordContent = string.Empty; // 清空輸入框
                _logger.LogInformation("已為設備 ID: {DeviceId} 添加新紀錄。", _deviceViewModel.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加紀錄時出錯。");
                MessageBox.Show($"添加紀錄失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddOrDeleteRecord))]
        private async Task DeleteRecordAsync()
        {
            if (SelectedRecord == null)
            {
                MessageBox.Show("請先選擇要刪除的紀錄。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"確定要刪除 ID 為 {SelectedRecord.Id} 的紀錄嗎？", "確認刪除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // 儲存要刪除的紀錄ID，以便稍後記錄
            var recordIdToDelete = SelectedRecord.Id;

            try
            {
                // **增加保護性檢查**
                if (_dbContext == null)
                {
                    _logger?.LogError("DeleteRecordAsync failed because _dbContext is null.");
                    MessageBox.Show("資料庫連線已遺失，無法刪除。", "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _dbContext.DeviceRecords.Remove(SelectedRecord);
                await _dbContext.SaveChangesAsync();

                DeviceRecords.Remove(SelectedRecord);

                // 使用先前儲存的 ID 來記錄
                _logger?.LogInformation("紀錄 ID: {RecordId} 已被刪除。", recordIdToDelete);
            }
            catch (Exception ex)
            {
                // **主要修正：使用 ?. 來安全地呼叫日誌**
                _logger?.LogError(ex, "刪除紀錄時出錯。");
                MessageBox.Show($"刪除紀錄失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanAddOrDeleteRecord()
        {
            // 可以在此加入權限判斷
            return true;
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            FilteredDeviceRecords.Refresh();
        }

        [RelayCommand]
        private void ResetFilter()
        {
            FilterUsername = null;
            FilterStartDate = null;
            FilteredDeviceRecords.Refresh();
        }

        private bool FilterRecords(object item)
        {
            if (item is not DeviceRecord record) return false;

            bool isUserMatch = string.IsNullOrWhiteSpace(FilterUsername) ||
                               (record.Username?.Contains(FilterUsername, StringComparison.OrdinalIgnoreCase) ?? false);

            bool isDateMatch = !FilterStartDate.HasValue ||
                               record.Timestamp.Date >= FilterStartDate.Value.Date;

            return isUserMatch && isDateMatch;
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel 檔案 (*.xlsx)|*.xlsx",
                Title = "匯出設備紀錄",
                FileName = $"{_deviceViewModel.Name}_紀錄_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true) return;

            string filePath = saveFileDialog.FileName;
            // 建立模板的完整路徑
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "導出模板", "CP08-003-02版-產品測試記錄表.xlsx");

            // 檢查模板是否存在
            if (!File.Exists(templatePath))
            {
                _logger.LogError("找不到 Excel 模板檔案於: {TemplatePath}", templatePath);
                MessageBox.Show($"模板檔案不存在，請確認路徑: {templatePath}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                ExcelPackage.License.SetNonCommercialOrganization("Sanjet");

                // **更正：從模板檔案載入 Excel Package**
                using var package = new ExcelPackage(new FileInfo(templatePath));

                // 假設資料要填入第一個工作表
                var worksheet = package.Workbook.Worksheets[0];

                // 根據您舊程式碼的邏輯，從第 4 列開始填寫資料
                int startRow = 4;
                int currentRow = startRow;

                // 根據時間升序排序，以便舊紀錄在前面
                var recordsToExport = FilteredDeviceRecords.Cast<DeviceRecord>().OrderBy(r => r.Timestamp);

                foreach (var record in recordsToExport)
                {
                    // 根據模板的欄位填入資料
                    worksheet.Cells[currentRow, 1].Value = record.Id;       // A欄: 排序
                    worksheet.Cells[currentRow, 2].Value = record.Timestamp;// B欄: 日期時間
                    worksheet.Cells[currentRow, 2].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                    worksheet.Cells[currentRow, 3].Value = record.DeviceName; // C欄: 機種
                    worksheet.Cells[currentRow, 4].Value = record.RunCount;   // D欄: 跑合
                    worksheet.Cells[currentRow, 5].Value = record.Content;    // E欄: 測試狀況
                    worksheet.Cells[currentRow, 6].Value = record.Username;   // F欄: 使用者
                    currentRow++;
                }

                // 自動調整欄寬
                //worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // 針對特定欄位自動調整欄寬
                worksheet.Column(1).AutoFit(); // A欄 (排序)
                worksheet.Column(2).AutoFit(); // B欄 (時間)
                worksheet.Column(3).AutoFit(); // C欄 (機種)
                worksheet.Column(4).AutoFit(); // D欄 (跑合)
                worksheet.Column(6).AutoFit(); // F欄 (使用者)

                // 設定 E 欄 (測試狀況) 的格式
                worksheet.Column(5).Width = 60; // 設定一個固定的寬度，例如 60
                worksheet.Column(5).Style.WrapText = true; // 啟用該欄的自動換行功能


                // **將修改後的模板內容另存為使用者指定的新檔案**
                package.SaveAs(new FileInfo(filePath));

                MessageBox.Show($"紀錄已成功匯出至: {filePath}", "匯出成功", MessageBoxButton.OK, MessageBoxImage.Information);

                // 詢問使用者是否開啟檔案
                if (MessageBox.Show("是否立即開啟匯出的檔案？", "操作確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // 需要 System.Diagnostics.Process
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "匯出 Excel 失敗。");
                MessageBox.Show($"匯出失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}