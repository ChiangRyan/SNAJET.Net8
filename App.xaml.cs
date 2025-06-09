using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SANJET.Core;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;
using SANJET.Core.Services; // 確保 MqttClientConnectionService 被引用
using SANJET.Core.ViewModels;
using SANJET.UI.Views.Windows;
using System.Threading.Tasks; // 新增
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop; // <--- 請新增此行

namespace SANJET
{
    public partial class App : Application
    {
        public static IHost? Host { get; private set; }
        private IMqttBrokerService? _mqttBrokerService;

        // 將 OnStartup 改為 async void
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                SQLitePCL.Batteries.Init();

                Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddLogging(configure => configure.AddDebug().SetMinimumLevel(LogLevel.Debug));

                        // 從 context.Configuration 讀取名為 "DefaultConnection" 的連接字串
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseSqlite(context.Configuration.GetConnectionString("DefaultConnection")));

                        services.AddScoped<IAuthenticationService, AuthenticationService>();
                        services.AddScoped<MainViewModel>();
                        services.AddScoped<HomeViewModel>();
                        services.AddScoped<SettingsPageViewModel>();

                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<LoginWindow>();
                        services.AddTransient<RecordWindow>();
                        services.AddTransient<LoadingWindow>(); // 註冊 LoadingWindow

                        services.AddSingleton<MainWindow>();
                        services.AddSingleton<IMqttService, MqttService>();
                        services.AddSingleton<IMqttBrokerService, MqttBrokerService>();
                        services.AddSingleton<IPollingStateService, PollingStateService>();
                        services.AddSingleton<INavigationService, NavigationService>();

                        services.AddHostedService<MqttClientConnectionService>();
                        services.AddHostedService<ModbusPollingService>();

                        services.AddLogging(builder =>
                        {
                            builder.AddConsole();
                            builder.AddDebug();
                            builder.SetMinimumLevel(LogLevel.Debug);
                        });
                    })
                    .Build();

                var appLogger = Host.Services.GetService<ILogger<App>>();

                _mqttBrokerService = Host.Services.GetRequiredService<IMqttBrokerService>();
                try
                {
                    await _mqttBrokerService.StartAsync();
                    appLogger?.LogInformation("MQTT Broker started successfully before Host.StartAsync().");
                }
                catch (Exception brokerEx)
                {
                    appLogger?.LogError(brokerEx, "Failed to start MQTT Broker before Host.StartAsync().");
                    MessageBox.Show($"MQTT Broker 啟動失敗：{brokerEx.Message}\n程式將繼續運行，但 MQTT 功能可能無法使用。",
                                    "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await Host.StartAsync();
                appLogger?.LogInformation("Application Host started.");

                var pollingStateSvc = Host.Services.GetRequiredService<IPollingStateService>();
                pollingStateSvc.DisablePolling();
                appLogger?.LogInformation("Polling explicitly disabled after Host start, before UI.");

                // --- 以下是新的啟動流程 ---

                // 1. 顯示載入視窗
                var loadingWindow = Host.Services.GetRequiredService<LoadingWindow>();
                loadingWindow.Show();

                // 2. 進行資料庫連線測試
                bool isConnected = await CheckDatabaseConnectionAsync(Host);

                // 3. 關閉載入視窗
                loadingWindow.Close();

                // 4. 根據連線結果決定下一步
                if (isConnected)
                {
                    appLogger?.LogInformation("Database connection successful. Initializing application.");

                    // 初始化資料庫 (EnsureCreated 和 SeedData)
                    using (var scope = Host.Services.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        SeedData(dbContext);
                    }

                    var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                    // mainWindow 的構造會觸發 MainViewModel 的構造，其中也會調用 DisablePolling
                    mainWindow.Show();

                    // 5. 顯示登入視窗 (不再設定 Owner)
                    var loginWindow = Host.Services.GetRequiredService<LoginWindow>();
                    loginWindow.Owner = mainWindow;
                    bool? loginDialogResult = loginWindow.ShowDialog(); // ShowDialog 會等待視窗關閉

                    // 6. 檢查登入結果
                    if (loginDialogResult == true)
                    {
                        if (mainWindow.DataContext is MainViewModel mainViewModel)
                        {
                            mainViewModel.UpdateLoginState(); // 登入成功後，UpdateLoginState 和後續導航會判斷是否啟用輪詢
                        }
                    }
                    else
                    {
                        // loginWindow.Close(); // DialogResult 會自動關閉，除非有特殊處理
                        // 如果登入失敗或取消，輪詢應保持禁用狀態
                        // 可以考慮再次調用 DisablePolling 以確保，但理論上 MainViewModel 的邏輯已覆蓋
                        var currentPollingState = Host.Services.GetRequiredService<IPollingStateService>();
                        if (currentPollingState.IsPollingEnabled) // 額外檢查
                        {
                            appLogger?.LogWarning("Login failed or cancelled, but polling was found enabled. Forcing disable.");
                            currentPollingState.DisablePolling();
                        }
                    }
                }
                else
                {
                    appLogger?.LogCritical("Database connection failed. Application will shut down.");
                    MessageBox.Show("無法連接到資料庫，請檢查網路連線或資料庫路徑設定。\n應用程式即將關閉。", "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>();
                logger?.LogError(ex, "應用程式啟動失敗");
                MessageBox.Show($"啟動失敗：{ex.Message}\n{ex.StackTrace}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// 檢查與資料庫的連線是否正常
        /// </summary>
        /// <param name="host">應用程式的 IHost</param>
        /// <returns>如果連線成功則返回 true，否則返回 false</returns>
        private async Task<bool> CheckDatabaseConnectionAsync(IHost host)
        {
            var logger = host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Checking database connection...");

            // 建立一個獨立的服務範圍來解析 AppDbContext
            // 這樣可以確保 DbContext 在檢查後被正確釋放
            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                // CanConnectAsync 是 EF Core 提供的非同步方法，用於測試連線
                if (await dbContext.Database.CanConnectAsync())
                {
                    logger.LogInformation("Database connection test successful.");
                    return true;
                }
                else
                {
                    logger.LogWarning("Database.CanConnectAsync returned false.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to the database.");
                return false;
            }
        }

        private static void SeedData(AppDbContext dbContext)
        {
            try
            {
                var logger = Host?.Services.GetService<ILogger<App>>();
                logger?.LogInformation("開始執行 SeedData...");

                dbContext.Database.EnsureCreated();
                logger?.LogInformation("資料庫已確保創建。");

                if (!dbContext.Users.Any())
                {
                    logger?.LogInformation("Users 表為空，開始插入預設資料...");

                    var adminUser = new User
                    {
                        Username = "administrator",
                        Password = "sanjet25653819",
                        Permissions = "ViewHome,ControlDevice,ViewSettings,All"
                    };
                    var user1 = new User
                    {
                        Username = "admin",
                        Password = "0000",
                        Permissions = "ViewHome,ControlDevice"
                    };
                    var user2 = new User
                    {
                        Username = "user",
                        Password = "0000",
                        Permissions = "ViewHome"
                    };
                    dbContext.Users.AddRange(adminUser, user1, user2);
                    dbContext.SaveChanges();

                    logger?.LogInformation("預設使用者已成功插入。");
                }
                else
                {
                    logger?.LogInformation("Users 表已有資料，跳過插入。");
                }

                // 新增 Devices 種子資料
                if (!dbContext.Devices.Any())
                {
                    logger?.LogInformation("Devices 表為空，開始插入預設設備資料...");
                    dbContext.Devices.AddRange(
                        new Device { Name = "預設設備1", ControllingEsp32MqttId = "ESP32_RS485", SlaveId = 1, Status = "閒置", IsOperational = true, RunCount = 0 },
                        new Device { Name = "預設設備2", ControllingEsp32MqttId = "ESP32_MdTCP", SlaveId = 1, Status = "運行中", IsOperational = true, RunCount = 150 }
                    );
                    logger?.LogInformation("預設設備已成功插入。");
                }
                else
                {
                    logger?.LogInformation("Devices 表已有資料，跳過設備插入。");
                }
                dbContext.SaveChanges();

            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>();
                logger?.LogError(ex, "SeedData 失敗");
                throw;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // 先停止 MQTT Broker
                if (_mqttBrokerService != null)
                {
                    _mqttBrokerService.StopAsync().GetAwaiter().GetResult();
                    var logger = Host?.Services.GetService<ILogger<App>>();
                    logger?.LogInformation("MQTT Broker 已停止");
                }
            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>();
                logger?.LogError(ex, "停止 MQTT Broker 時發生錯誤");
            }
            finally
            {
                Host?.StopAsync().GetAwaiter().GetResult();
                Host?.Dispose();
                base.OnExit(e);
            }
        }
    }
}