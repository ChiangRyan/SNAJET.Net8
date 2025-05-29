using Microsoft.EntityFrameworkCore;
using SANJET.Core.Models;

namespace SANJET.Core
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Device> Devices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // 建議保留對基底方法的呼叫

            modelBuilder.Entity<User>()
                .Property(u => u.Username)
                .IsRequired();
            modelBuilder.Entity<User>()
                .Property(u => u.Password)
                .IsRequired();
            modelBuilder.Entity<User>()
                .Property(u => u.Permissions)
                .IsRequired(false);


            // 設定 Device 模型的相關約束 (如果需要)
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.Id); // 確認主鍵
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100); // 例如，名稱必填且最大長度100
                entity.Property(e => e.ControllingEsp32MqttId).IsRequired();

                // 可以為其他屬性添加更多設定，如索引、預設值等   
                // entity.HasIndex(e => new { e.ControllingEsp32MqttId, e.SlaveId }).IsUnique();
            });



        }




    }
}