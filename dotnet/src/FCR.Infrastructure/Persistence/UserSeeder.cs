using FCR.Domain;
using FCR.Domain.Entities;
using FCR.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace FCR.Infrastructure.Persistence;

/// <summary>
/// Seeds the FCR module and workflow roles and, when the users table is empty, a single
/// development System Administrator account (password from Fcr:SeedAdminPassword).
/// Dev-auth only — production credentials come from the company's chosen provider.
/// </summary>
public static class UserSeeder
{
    public const string DefaultAdminPassword = "admin";

    public static async Task SeedAsync(FcrDbContext db, string? adminPassword)
    {
        foreach (var name in RoleNames.All)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == name))
                db.Roles.Add(new Role { Name = name });
        }
        await db.SaveChangesAsync();

        if (await db.Users.AnyAsync()) return;

        var adminRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.SystemAdministrator);
        var admin = new User
        {
            Username = "admin",
            DisplayName = "System Administrator",
            PasswordHash = PasswordHasher.Hash(
                string.IsNullOrWhiteSpace(adminPassword) ? DefaultAdminPassword : adminPassword),
            IsActive = true,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync();
    }
}
