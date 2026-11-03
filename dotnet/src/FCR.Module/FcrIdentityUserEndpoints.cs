using FCR.Application;
using FCR.Domain;

namespace FCR.Module;

/// <summary>FCR Admin management of ERMS identity mappings and FCR roles.</summary>
public static class FcrIdentityUserEndpoints
{
    public static void MapFcrIdentityUsers(this WebApplication app)
    {
        var users = app.MapGroup("/api/admin/fcr-users")
            .RequireAuthorization(PolicyNames.AdminConsole);

        users.MapGet("/", async (IUserService service) =>
            Results.Ok(await service.ListUsersAsync()));

        users.MapPost("/", async Task<IResult> (
            CreateExternalUserRequest input, IUserService service) =>
        {
            try
            {
                return Results.Json(await service.CreateExternalUserAsync(input), statusCode: 201);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });

        users.MapPut("/{id:long}", async Task<IResult> (
            long id, UpdateUserRequest input, IUserService service) =>
        {
            try
            {
                var user = await service.UpdateUserAsync(id, input);
                return user is null ? Detail(404, "FCR user not found") : Results.Ok(user);
            }
            catch (FcrValidationException ex) { return Detail(400, ex.Message); }
        });
    }

    private static IResult Detail(int status, string message) =>
        Results.Json(new Dictionary<string, object?> { ["detail"] = message }, statusCode: status);
}
