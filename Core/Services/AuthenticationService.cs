using Microsoft.EntityFrameworkCore;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;

namespace SANJET.Core.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly AppDbContext _dbContext;
        // private string? _currentUser; // <-- 改為儲存 User 物件
        private User? _loggedInUser;

        public AuthenticationService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User?> GetUserWithPermissionsAsync(string username, string password)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username && u.Password == password);
            if (user != null)
            {
                // _currentUser = username; // <-- 改為設定 _loggedInUser
                _loggedInUser = user;
                user.PermissionsList = string.IsNullOrEmpty(user.Permissions)
                    ? [] // 如果 Permissions 為空，則初始化為空列表
                    : [.. user.Permissions.Split(',')]; //
                return user;
            }
            _loggedInUser = null; // 登入失敗則清空
            return null;
        }

        public User? GetCurrentUser() // <-- 修改返回類型和實現
        {
            return _loggedInUser;
        }

        public void Logout()
        {
            // _currentUser = null; // <-- 改為清空 _loggedInUser
            _loggedInUser = null; //
        }
    }
}