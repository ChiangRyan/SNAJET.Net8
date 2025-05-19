using SANJET.Core.Models;

namespace SANJET.Core.Interfaces
{
    public interface IAuthenticationService
    {
        Task<User?> GetUserWithPermissionsAsync(string username, string password);
        string? GetCurrentUser();
        void Logout();
    }
}