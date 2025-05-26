namespace SANJET.Core.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Permissions { get; set; }
        public List<string> PermissionsList { get; set; } = [];
    }
}