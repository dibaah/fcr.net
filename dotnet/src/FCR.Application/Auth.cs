namespace FCR.Application;

public record LoginRequest(string Username, string Password);
public record UserDto(long Id, string Username, string DisplayName, string Email,
    string? ExternalId, bool IsActive, List<string> Roles);
public record CreateExternalUserRequest(string ExternalId, string Username, string DisplayName,
    string Email, List<string>? Roles, bool IsActive = true);
public record RoleDto(long Id, string Name, string Description);
public record CreateUserRequest(string Username, string DisplayName, string Email,
    string Password, List<string>? Roles, bool IsActive = true);
public record UpdateUserRequest(string? DisplayName, string? Email,
    bool? IsActive, List<string>? Roles, string? Password);

public interface IUserService
{
    Task<UserDto?> VerifyCredentialsAsync(string username, string password, CancellationToken ct = default);
    Task<bool> IsActiveUserAsync(long id, CancellationToken ct = default);
    Task<List<string>> GetRoleNamesAsync(long userId, CancellationToken ct = default);
    Task<List<UserDto>> ListUsersAsync(CancellationToken ct = default);
    Task<UserDto?> GetUserAsync(long id, CancellationToken ct = default);
    Task<UserDto?> GetUserByExternalIdAsync(string externalId, CancellationToken ct = default);
    Task<UserDto> CreateExternalUserAsync(CreateExternalUserRequest req, CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest req, CancellationToken ct = default);
    Task<UserDto?> UpdateUserAsync(long id, UpdateUserRequest req, CancellationToken ct = default);
    Task<List<RoleDto>> ListRolesAsync(CancellationToken ct = default);
}
