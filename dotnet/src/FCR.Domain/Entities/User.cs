namespace FCR.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    /// <summary>Stable external ERMS subject identifier; null for standalone local users.</summary>
    public string? ExternalId { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public List<UserRole> UserRoles { get; set; } = [];
}

public class Role
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<UserRole> UserRoles { get; set; } = [];
}

public class UserRole
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public User? User { get; set; }
    public Role? Role { get; set; }
}
