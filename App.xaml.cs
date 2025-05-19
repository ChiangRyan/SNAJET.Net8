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
                Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseSqlite("Data Source=sanjet.db"));

                        services.AddScoped<IAuthenticationService, AuthenticationService>();
                        services.AddScoped<MainViewModel>(provider =>
                        {
                            var frame = (Frame)provider.GetRequiredService<MainWindow>().FindName("MainContentFrame");
                            return new MainViewModel(
                                provider.GetRequiredService<IAuthenticationService>(),
                                frame
                            );
                        });
                        services.AddScoped<LoginViewModel>();
                        services.AddTransient<LoginWindow>(provider =>
                        {
                            return new LoginWindow(provider.GetRequiredService<LoginViewModel>());
                        });
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
                    var loginWindow = Host.Services.GetRequiredService<LoginWindow>();
                    loginWindow.Show();
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
                dbContext.Database.EnsureCreated();

                if (!dbContext.Users.Any())
                {
                    var adminUser = new User
                    {
                        Username = "administrator",
                        Password = "sanjet25653819",
                        Permissions = "ViewHome,ViewManualOperation,ViewMonitor,ViewWarning,ViewSettings,ControlDevice,All"
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
                }
            }
            catch (Exception ex)
            {
                var logger = Host?.Services.GetService<ILogger<App>>() ?? throw new InvalidOperationException("無法獲取日誌服務");
                logger.LogError(ex, "SeedData 失敗");
                throw;
            }
        }
    }
}