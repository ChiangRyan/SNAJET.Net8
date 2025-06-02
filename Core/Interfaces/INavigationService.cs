using System.Threading.Tasks;
using System.Windows.Controls;

namespace SANJET.Core.Interfaces
{
    public interface INavigationService
    {
        Task NavigateToHomeAsync(Frame frame);
        void NavigateToSettings(Frame frame);
        void ClearNavigation(Frame frame);
    }
}