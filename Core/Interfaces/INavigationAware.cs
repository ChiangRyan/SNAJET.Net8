// 檔案路徑: SANJET.Core/Interfaces/INavigationAware.cs
using System.Threading.Tasks;

namespace SANJET.Core.Interfaces
{
    public interface INavigationAware
    {
        /// <summary>
        /// 當導航到此 ViewModel 時調用。
        /// </summary>
        /// <param name="parameter">導航時傳遞的參數（可選）。</param>
        Task OnNavigatedToAsync(object? parameter);

        /// <summary>
        /// 當從此 ViewModel 導航離開時調用。
        /// </summary>
        Task OnNavigatedFromAsync();
    }
}