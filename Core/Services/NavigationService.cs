using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SANJET.Core.Constants.Enums;
using SANJET.Core.Interfaces;
using SANJET.Core.ViewModels;
using SANJET.UI.Views.Pages;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SANJET.Core.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NavigationService> _logger;

        public NavigationService(IServiceProvider serviceProvider, ILogger<NavigationService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task NavigateToHomeAsync(Frame? frame)
        {
            if (frame == null)
            {
                _logger.LogWarning("導航到首頁失敗：Frame 為 null。");
                return;
            }

            // 檢查當前頁面是否為 HomePage 且 DataContext 正確
            if (frame.Content is HomePage homePage && homePage.DataContext is HomeViewModel currentViewModel)
            {
                _logger.LogInformation("已在首頁，僅刷新數據。");
                currentViewModel.CanControlDevice = GetCanControlDevice();
                await currentViewModel.LoadDevicesAsync();
                return;
            }

            // 創建新頁面並設置 ViewModel
            var homePageInstance = _serviceProvider.GetService<HomePage>() ?? new HomePage();
            var homeViewModel = _serviceProvider.GetService<HomeViewModel>();

            if (homeViewModel != null)
            {
                homeViewModel.CanControlDevice = GetCanControlDevice();
                await homeViewModel.LoadDevicesAsync();
                homePageInstance.DataContext = homeViewModel;
                _logger.LogInformation("已為首頁設置 HomeViewModel 並加載設備數據。");
            }
            else
            {
                _logger.LogWarning("無法從 IServiceProvider 獲取 HomeViewModel，首頁將無 DataContext。");
            }

            frame.Navigate(homePageInstance);
            _logger.LogInformation("成功導航到首頁，Frame 內容類型：{ContentType}", frame.Content?.GetType().Name);
        }

        public void NavigateToSettings(Frame? frame)
        {
            if (frame == null)
            {
                _logger.LogWarning("導航到設定頁失敗：Frame 為 null。");
                return;
            }

            // 檢查當前頁面是否為 SettingsPage 且 DataContext 正確
            if (frame.Content is SettingsPage settingsPage && settingsPage.DataContext is SettingsPageViewModel currentViewModel)
            {
                _logger.LogInformation("已在設定頁，僅刷新設定數據。");
                currentViewModel.LoadSettings();
                return;
            }

            // 創建新頁面並設置 ViewModel
            var settingsPageInstance = _serviceProvider.GetService<SettingsPage>() ?? new SettingsPage();
            var settingsViewModel = _serviceProvider.GetService<SettingsPageViewModel>();

            if (settingsViewModel != null)
            {
                settingsViewModel.LoadSettings();
                settingsPageInstance.DataContext = settingsViewModel;
                _logger.LogInformation("已為設定頁設置 SettingsPageViewModel 並加載設定。");
            }
            else
            {
                _logger.LogWarning("無法從 IServiceProvider 獲取 SettingsPageViewModel，設定頁將無 DataContext。");
            }

            frame.Navigate(settingsPageInstance);
            _logger.LogInformation("成功導航到設定頁，Frame 內容類型：{ContentType}", frame.Content?.GetType().Name);
        }

        public void ClearNavigation(Frame? frame)
        {
            if (frame == null)
            {
                _logger.LogWarning("無法清除導航：Frame 為 null。");
                return;
            }

            frame.Navigate(null);
            _logger.LogInformation("已清除 Frame 內容。");
        }

        private bool GetCanControlDevice()
        {
            var authService = _serviceProvider.GetService<IAuthenticationService>();
            var user = authService?.GetCurrentUser();
            return user != null && (user.PermissionsList?.Contains(Permission.ControlDevice.ToString()) == true ||
                                   user.PermissionsList?.Contains(Permission.All.ToString()) == true);
        }
    }
}