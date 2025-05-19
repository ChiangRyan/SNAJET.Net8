using Microsoft.EntityFrameworkCore;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;

namespace SANJET.Core.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly AppDbContext _dbContext;
        private string? _currentUser;

        public AuthenticationService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User?> GetUserWithPermissionsAsync(string username, string password)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username && u.Password == password);
            if (user != null)
            {
                _currentUser = username;
                user.PermissionsList = string.IsNullOrEmpty(user.Permissions)
                    ? new List<string>()
                    : user.Permissions.Split(',').ToList();
                return user;
            }
            return null;
        }

        public string? GetCurrentUser()
        {
            return _currentUser;
        }

        public void Logout()
        {
            _currentUser = null;
        }
    }
}