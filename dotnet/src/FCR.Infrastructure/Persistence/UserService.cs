using FCR.Application;
using FCR.Domain;
using FCR.Domain.Entities;
using FCR.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace FCR.Infrastructure.Persistence;

/// <summary>
/// User/role management and development-auth credential checks against the
/// users/roles/user_roles tables. Role names are validated against RoleNames.All —
/// permissions are never hardcoded to user names.
/// </summary>
public sealed class UserService(FcrDbContext db) : IUserService
{
    public async Task<UserDto?> VerifyCredentialsAsync(
        string username, string password, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null || !user.IsActive) return null;
        if (!PasswordHasher.Verify(password, user.PasswordHash)) return null;
        return ToDto(user);
    }

    public Task<bool> IsActiveUserAsync(long id, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Id == id && u.IsActive, ct);

    public Task<List<string>> GetRoleNamesAsync(long userId, CancellationToken ct = default) =>
        db.UserRoles.Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role!.Name).ToListAsync(ct);

    public Task<List<UserDto>> ListUsersAsync(CancellationToken ct = default) =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username).Select(u => ToDto(u)).ToListAsync(ct);

    public Task<UserDto?> GetUserAsync(long id, CancellationToken ct = default) =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.Id == id).Select(u => ToDto(u)).FirstOrDefaultAsync(ct);

    public Task<UserDto?> GetUserByExternalIdAsync(string externalId, CancellationToken ct = default) =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ExternalId == externalId)
            .Select(u => ToDto(u)).FirstOrDefaultAsync(ct);

    public async Task<UserDto> CreateExternalUserAsync(
        CreateExternalUserRequest req, CancellationToken ct = default)
    {
        var externalId = (req.ExternalId ?? "").Trim();
        var username = (req.Username ?? "").Trim();
        if (externalId.Length == 0) throw new FcrValidationException("External identity required");
        if (username.Length == 0) throw new FcrValidationException("Username required");
        if (await db.Users.AnyAsync(u => u.ExternalId == externalId, ct))
            throw new FcrValidationException("External identity is already mapped");
        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            throw new FcrValidationException($"Username '{username}' already exists");

        var user = new User
        {
            ExternalId = externalId,
            Username = username,
            DisplayName = req.DisplayName?.Trim() ?? "",
            Email = req.Email?.Trim() ?? "",
            // External users authenticate through ERMS; this value is intentionally
            // not a valid local password hash and cannot be used for local login.
            PasswordHash = "!external-id-only!",
            IsActive = req.IsActive,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await AssignRolesAsync(user, req.Roles, ct);
        await db.SaveChangesAsync(ct);
        return (await GetUserAsync(user.Id, ct))!;
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        var username = (req.Username ?? "").Trim();
        if (username.Length == 0) throw new FcrValidationException("Username required");
        if (string.IsNullOrWhiteSpace(req.Password))
            throw new FcrValidationException("Password required");
        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            throw new FcrValidationException($"Username '{username}' already exists");

        var user = new User
        {
            Username = username,
            DisplayName = req.DisplayName?.Trim() ?? "",
            Email = req.Email?.Trim() ?? "",
            PasswordHash = PasswordHasher.Hash(req.Password),
            IsActive = req.IsActive,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await AssignRolesAsync(user, req.Roles, ct);
        await db.SaveChangesAsync(ct);
        return (await GetUserAsync(user.Id, ct))!;
    }

    public async Task<UserDto?> UpdateUserAsync(
        long id, UpdateUserRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return null;

        if (req.DisplayName is not null) user.DisplayName = req.DisplayName.Trim();
        if (req.Email is not null) user.Email = req.Email.Trim();
        if (req.IsActive.HasValue) user.IsActive = req.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = PasswordHasher.Hash(req.Password);
        if (req.Roles is not null)
        {
            db.UserRoles.RemoveRange(user.UserRoles);
            await AssignRolesAsync(user, req.Roles, ct);
        }
        user.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        await db.SaveChangesAsync(ct);
        return await GetUserAsync(id, ct);
    }

    public Task<List<RoleDto>> ListRolesAsync(CancellationToken ct = default) =>
        db.Roles.OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description)).ToListAsync(ct);

    private async Task AssignRolesAsync(User user, List<string>? roles, CancellationToken ct)
    {
        foreach (var name in roles ?? [])
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == name, ct)
                ?? throw new FcrValidationException($"Unknown role '{name}'");
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }
    }

    private static UserDto ToDto(User u) => new(
        u.Id, u.Username, u.DisplayName, u.Email, u.ExternalId, u.IsActive,
        u.UserRoles.Where(ur => ur.Role is not null).Select(ur => ur.Role!.Name).ToList());
}
