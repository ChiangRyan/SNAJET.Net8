using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration; // 新增這個 using
using SQLitePCL;
using System.IO; // 新增這個 using

namespace SANJET.Core
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            Batteries.Init();

            // 手動建立 Configuration 來讀取 appsettings.json
            IConfigurationRoot configuration = new ConfigurationBuilder()
                // 設定基礎路徑為專案的輸出目錄
                .SetBasePath(Directory.GetCurrentDirectory())
                // 讀取 appsettings.json 檔案
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            // 從 configuration 讀取連接字串
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseSqlite(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}