using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SANJET.Core;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;
using SANJET.Core.Services; // 確保 MqttClientConnectionService 被引用
using SANJET.Core.ViewModels;
using SANJET.UI.Views.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks; // 新增

namespace SANJET
{
    public partial class App : Application
    {
        public static IHost? Host { get; private set; }
        private IMqttBrokerService? _mqttBrokerService;

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
                            options.UseSqlite("Data Source=sanjet.db"));
                        services.AddScoped<IAuthenticationService, AuthenticationService>();
                        services.AddScoped<MainViewModel>();
                        services.AddScoped<HomeViewModel>();
                        services.AddScoped<SettingsPageViewModel>();

                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<LoginWindow>();
                        services.AddTransient<RecordView>();


                        services.AddSingleton<MainWindow>();
                        services.AddSingleton<IMqttService, MqttService>();
                        services.AddSingleton<IMqttBrokerService, MqttBrokerService>(); 
                        services.AddSingleton<IPollingStateService, PollingStateService>();
                        services.AddSingleton<INavigationService, NavigationService>();

                        // 註冊新的背景服務
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

                // 1. 手動解析並啟動 MQTT Broker 服務 (在 Host.StartAsync() 之前)
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

                // 2. 啟動 Host (這將會啟動 MqttClientConnectionService 等 IHostedService)
                await Host.StartAsync(); // 從 GetAwaiter().GetResult() 改為 await
                appLogger?.LogInformation("Application Host started.");

                var pollingStateSvc = Host.Services.GetRequiredService<IPollingStateService>();
                pollingStateSvc.DisablePolling();
                appLogger?.LogInformation("Polling explicitly disabled after Host start, before UI.");

                // 3. 初始化資料庫
                using (var scope = Host.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    SeedData(dbContext); // SeedData 內若使用 Host.Services.GetService<ILogger<App>>() 則 Host 需已建立
                }

                // 移除原先手動啟動 MQTT Broker 的邏輯，因為已提前啟動
                // await StartMqttBrokerAsync().GetAwaiter().GetResult(); // 移除或註解此行


                if (Host != null)
                {
                    var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                    // mainWindow 的構造會觸發 MainViewModel 的構造，其中也會調用 DisablePolling
                    mainWindow.Show();

                    var loginWindow = Host.Services.GetRequiredService<LoginWindow>();
                    loginWindow.Owner = mainWindow;
                    bool? loginDialogResult = loginWindow.ShowDialog(); // 此時輪詢應已確認為禁用

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
            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>() ?? throw new InvalidOperationException("無法獲取日誌服務");
                logger.LogError(ex, "應用程式啟動失敗");
                MessageBox.Show($"啟動失敗：{ex.Message}\n{ex.StackTrace}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }

            // base.OnStartup(e); // WPF 的 Application.OnStartup 是 void，若要呼叫 base，async void 可能不適合直接 base.OnStartup
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