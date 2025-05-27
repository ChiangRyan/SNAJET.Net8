using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SANJET.Core;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;
using SANJET.Core.Services;
using SANJET.Core.ViewModels;
using SANJET.UI.Views.Windows;
using System.Windows;
using System.Windows.Controls;

namespace SANJET
{
    public partial class App : Application
    {
        public static IHost? Host { get; private set; }
        private IMqttBrokerService? _mqttBrokerService;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                SQLitePCL.Batteries.Init(); // 確保 SQLite 初始化

                Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        // Logger
                        services.AddLogging(configure => configure.AddDebug().SetMinimumLevel(LogLevel.Debug));

                        services.AddDbContext<AppDbContext>(options =>
                            options.UseSqlite("Data Source=sanjet.db"));

                        services.AddScoped<IAuthenticationService, AuthenticationService>();
                        services.AddScoped<MainViewModel>();
                        services.AddScoped<HomeViewModel>();

                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<LoginWindow>();

                        services.AddSingleton<MainWindow>();

                        // 註冊 MQTT 服務
                        services.AddSingleton<IMqttService, MqttService>();
                        services.AddSingleton<IMqttBrokerService, MqttBrokerService>();

                        services.AddLogging(builder =>
                        {
                            builder.AddConsole();
                            builder.AddDebug();
                            builder.SetMinimumLevel(LogLevel.Debug);
                        });
                    })
                    .Build();

                // 啟動 Host
                Host.StartAsync().GetAwaiter().GetResult();

                // 初始化資料庫
                using (var scope = Host.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    SeedData(dbContext);
                }

                // 啟動 MQTT Broker
                StartMqttBrokerAsync().GetAwaiter().GetResult();

                if (Host != null)
                {
                    var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();

                    var loginWindow = Host.Services.GetRequiredService<LoginWindow>();
                    loginWindow.Owner = mainWindow;
                    bool? loginDialogResult = loginWindow.ShowDialog();

                    if (loginDialogResult == true) // 登入成功
                    {
                        if (mainWindow.DataContext is MainViewModel mainViewModel)
                        {
                            mainViewModel.UpdateLoginState();
                        }
                    }
                    else // 登入失敗或取消
                    {
                        loginWindow.Close();
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

            base.OnStartup(e);
        }

        private async Task StartMqttBrokerAsync()
        {
            try
            {
                if (Host != null)
                {
                    _mqttBrokerService = Host.Services.GetRequiredService<IMqttBrokerService>();
                    await _mqttBrokerService.StartAsync();

                    var logger = Host.Services.GetService<ILogger<App>>();
                    logger?.LogInformation("MQTT Broker 啟動成功");
                }
            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>();
                logger?.LogError(ex, "啟動 MQTT Broker 失敗");

                // 顯示錯誤但不終止程式
                MessageBox.Show($"MQTT Broker 啟動失敗：{ex.Message}\n程式將繼續運行，但 MQTT 功能可能無法使用。",
                    "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        Permissions = "ViewHome,ControlDevice,All"
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
                        new Device { Name = "預設設備1", IpAddress = "192.168.1.200", SlaveId = 10, Status = "閒置", IsOperational = true, RunCount = 0 },
                        new Device { Name = "預設設備2", IpAddress = "192.168.1.201", SlaveId = 11, Status = "運行中", IsOperational = true, RunCount = 150 }
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