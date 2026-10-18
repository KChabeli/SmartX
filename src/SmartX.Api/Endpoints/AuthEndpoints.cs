using SmartX.Api.Contracts;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", (LoginRequest request, AuthService authService, RealtimeMeshEngine engine) =>
        {
            try
            {
                return Results.Ok(authService.Login(request.MacAddress, engine));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        });

        auth.MapGet("/me", (HttpContext http, AuthService authService) =>
        {
            if (!AuthService.TryReadToken(http, out var token) || authService.Find(token) is not { } session)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(session);
        });

        auth.MapPost("/logout", (HttpContext http, AuthService authService) =>
        {
            if (AuthService.TryReadToken(http, out var token))
            {
                authService.Logout(token);
            }

            return Results.NoContent();
        });

        return app;
    }

    public static RouteHandlerBuilder RequireOperator(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var auth = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
            if (!AuthService.TryReadToken(context.HttpContext, out var token) || auth.Find(token) is null)
            {
                return Results.Unauthorized();
            }

            return await next(context);
        });
}
