// 檔案路徑: App.xaml.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SANJET.Core;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;
using SANJET.Core.Services;
using SANJET.Core.ViewModels;
using SANJET.UI.Views.Windows;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading; // 為了 DispatcherUnhandledException

namespace SANJET
{
    public partial class App : Application
    {
        public static IHost? Host { get; private set; }
        private IMqttBrokerService? _mqttBrokerService;

        /// <summary>
        /// 應用程式建構函式：在此處訂閱全域未處理例外事件。
        /// </summary>
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        /// <summary>
        /// 全域例外處理器：捕捉任何在 UI 執行緒上未被處理的錯誤。
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = Host?.Services.GetService<ILogger<App>>();
            logger?.LogError(e.Exception, "捕獲到一個未處理的全域例外");

            MessageBox.Show("捕獲到未處理的例外狀況，應用程式即將關閉。\n\n" +
                            $"錯誤訊息: {e.Exception.Message}\n\n" +
                            $"內部例外: {e.Exception.InnerException?.Message}\n\n" +
                            $"堆疊追蹤: {e.Exception.StackTrace}",
                            "未處理的例外狀況",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

            e.Handled = true;
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 應用程式啟動主邏輯。
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                SQLitePCL.Batteries.Init();

                Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddLogging(configure => configure.AddDebug().SetMinimumLevel(LogLevel.Debug));
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection")));
                        services.AddScoped<IAuthenticationService, AuthenticationService>();
                        services.AddScoped<MainViewModel>();
                        services.AddScoped<HomeViewModel>();
                        services.AddScoped<SettingsPageViewModel>();
                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<LoginWindow>();
                        services.AddTransient<RecordWindow>();
                        services.AddTransient<LoadingWindow>();
                        services.AddSingleton<MainWindow>();
                        services.AddSingleton<IMqttService, MqttService>();
                        services.AddSingleton<IMqttBrokerService, MqttBrokerService>();
                        services.AddSingleton<IPollingStateService, PollingStateService>();
                        services.AddSingleton<INavigationService, NavigationService>();
                        services.AddHostedService<MqttClientConnectionService>();
                        services.AddHostedService<ModbusPollingService>();
                    })
                    .Build();

                var appLogger = Host.Services.GetRequiredService<ILogger<App>>();

                _mqttBrokerService = Host.Services.GetRequiredService<IMqttBrokerService>();
                try
                {
                    await _mqttBrokerService.StartAsync();
                    appLogger.LogInformation("MQTT Broker started successfully.");
                }
                catch (Exception brokerEx)
                {
                    appLogger.LogError(brokerEx, "Failed to start MQTT Broker.");
                    MessageBox.Show($"MQTT Broker 啟動失敗：{brokerEx.Message}\n程式將繼續運行，但 MQTT 功能可能無法使用。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await Host.StartAsync();
                appLogger.LogInformation("Application Host started.");

                Host.Services.GetRequiredService<IPollingStateService>().DisablePolling();
                appLogger.LogInformation("Polling explicitly disabled on startup.");

                // 顯示載入視窗
                var loadingWindow = Host.Services.GetRequiredService<LoadingWindow>();
                loadingWindow.Show();

                // 執行需要時間的啟動任務
                bool isConnected = await CheckDatabaseConnectionAsync(Host, appLogger);

                // 任務完成後關閉載入視窗
                loadingWindow.Close();

                if (isConnected)
                {
                    appLogger.LogInformation("Database connection successful. Initializing main application.");
                    using (var scope = Host.Services.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await dbContext.Database.EnsureCreatedAsync();
                        SeedData(dbContext, appLogger);
                    }

                    // 顯示主視窗。登入邏輯將由 MainWindow 的 Loaded 事件觸發。
                    var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                    Application.Current.MainWindow = mainWindow; // 明確設定應用程式的主視窗
                    mainWindow.Show();
                }
                else
                {
                    appLogger.LogCritical("Database connection failed. Application will shut down.");
                    MessageBox.Show("無法連接到資料庫，請檢查網路連線或資料庫路徑設定。\n應用程式即將關閉。", "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>();
                logger?.LogError(ex, "應用程式啟動時發生無法處理的錯誤");
                MessageBox.Show($"啟動失敗：{ex.Message}\n{ex.StackTrace}", "致命錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// 檢查與資料庫的連線是否正常。
        /// </summary>
        private async Task<bool> CheckDatabaseConnectionAsync(IHost host, ILogger<App> logger)
        {
            logger.LogInformation("Checking database connection...");
            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                return await dbContext.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to the database.");
                return false;
            }
        }

        /// <summary>
        /// 初始化資料庫的種子資料。
        /// </summary>
        private void SeedData(AppDbContext dbContext, ILogger<App> logger)
        {
            try
            {
                logger.LogInformation("開始執行 SeedData...");

                if (!dbContext.Users.Any())
                {
                    logger.LogInformation("Users 表為空，開始插入預設資料...");
                    dbContext.Users.AddRange(
                        new User { Username = "administrator", Password = "sanjet25653819", Permissions = "ViewHome,ControlDevice,ViewSettings,All" },
                        new User { Username = "admin", Password = "0000", Permissions = "ViewHome,ControlDevice" },
                        new User { Username = "user", Password = "0000", Permissions = "ViewHome" }
                    );
                }
                else
                {
                    logger.LogInformation("Users 表已有資料，跳過插入。");
                }

                if (!dbContext.Devices.Any())
                {
                    logger.LogInformation("Devices 表為空，開始插入預設設備資料...");
                    dbContext.Devices.AddRange(
                        new Device { Name = "預設設備1", ControllingEsp32MqttId = "ESP32_RS485", SlaveId = 1, Status = "閒置", IsOperational = true, RunCount = 0 },
                        new Device { Name = "預設設備2", ControllingEsp32MqttId = "ESP32_MdTCP", SlaveId = 1, Status = "運行中", IsOperational = true, RunCount = 150 }
                    );
                }
                else
                {
                    logger.LogInformation("Devices 表已有資料，跳過設備插入。");
                }
                dbContext.SaveChanges();
                logger.LogInformation("SeedData 完成。");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SeedData 失敗");
                throw; // 拋出例外，由上層的 try-catch 處理
            }
        }

        /// <summary>
        /// 應用程式關閉時的清理工作。
        /// </summary>
        protected override async void OnExit(ExitEventArgs e)
        {
            if (_mqttBrokerService != null)
            {
                await _mqttBrokerService.StopAsync();
            }

            if (Host != null)
            {
                await Host.StopAsync();
                Host.Dispose();
            }
            base.OnExit(e);
        }
    }
}