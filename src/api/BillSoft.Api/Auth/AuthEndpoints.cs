using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BillSoft.Application.Auth;
using BillSoft.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BillSoft.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth-login-fixed");

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous();

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous();

        group.MapGet("/me", Me)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest? request,
        IAuthService authService,
        HttpContext httpContext)
    {
        try
        {
            var result = await authService.LoginAsync(request, ResolveIpAddress(httpContext), httpContext.RequestAborted);
            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized("Invalid credentials.");
        }
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest? request,
        IAuthService authService,
        HttpContext httpContext)
    {
        try
        {
            var result = await authService.RefreshAsync(request, ResolveIpAddress(httpContext), httpContext.RequestAborted);
            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized("Invalid or expired refresh token.");
        }
    }

    private static async Task<IResult> LogoutAsync(
        RefreshTokenRequest? request,
        IAuthService authService,
        HttpContext httpContext)
    {
        await authService.LogoutAsync(request, ResolveIpAddress(httpContext), httpContext.RequestAborted);
        return Results.NoContent();
    }

    private static IResult Me(
        ClaimsPrincipal principal,
        IAuthService authService)
    {
        var result = authService.GetCurrentUserContext(principal);
        return Results.Ok(result);
    }

    private static string ResolveIpAddress(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(address) ? "unknown" : address.Trim();
    }

    private static IResult Unauthorized(string detail) =>
        Results.Problem(
            title: "Unauthorized",
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            type: "https://datatracker.ietf.org/doc/html/rfc7807");
}
