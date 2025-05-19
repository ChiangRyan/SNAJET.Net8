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

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                SQLitePCL.Batteries.Init(); // 確保 SQLite 初始化

                Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseSqlite("Data Source=sanjet.db"));

                        services.AddScoped<IAuthenticationService, AuthenticationService>();
                        services.AddScoped<MainViewModel>();
                        services.AddScoped<LoginViewModel>();
                        services.AddTransient<LoginWindow>(); // 改為 Transient，因為每個登入視窗應是新的實例
                        services.AddSingleton<MainWindow>();

                        services.AddLogging(builder =>
                        {
                            builder.AddConsole();
                            builder.AddDebug();
                            builder.SetMinimumLevel(LogLevel.Debug);
                        });
                    })
                    .Build();

                Host.StartAsync().GetAwaiter().GetResult();

                using (var scope = Host.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    SeedData(dbContext);
                }

                if (Host != null)
                {
                    // 顯示 MainWindow
                    var mainWindow = Host.Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();

                    // 顯示 LoginWindow 作為模態視窗
                    var loginWindow = Host.Services.GetRequiredService<LoginWindow>();
                    loginWindow.Owner = mainWindow; // 設定 MainWindow 為 LoginWindow 的擁有者
                    loginWindow.ShowDialog(); // 使用 ShowDialog 顯示模態視窗

                    // 如果登入失敗，關閉應用程式
                    if (loginWindow.DialogResult != true)
                    {
                        loginWindow.Close();
                        //Shutdown();
                    }
                }
                else
                {
                    throw new InvalidOperationException("Host 未正確初始化");
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
        protected override void OnExit(ExitEventArgs e)
        {
            Host?.StopAsync().GetAwaiter().GetResult();
            Host?.Dispose();
            base.OnExit(e);
        }

        private void SeedData(AppDbContext dbContext)
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
            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>();
                logger?.LogError(ex, "SeedData 失敗");
                throw; // 保留異常以便調試
            }
        }


    }
}