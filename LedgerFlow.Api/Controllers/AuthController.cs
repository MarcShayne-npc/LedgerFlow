using LedgerFlow.Application.Common;
using LedgerFlow.Infrastructure.Auth;
using LedgerFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LedgerFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtTokenIssuer _tokens;

    public AuthController(UserManager<ApplicationUser> users, JwtTokenIssuer tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized();

        var valid = await _users.CheckPasswordAsync(user, request.Password);
        if (!valid)
            return Unauthorized();

        var token = await _tokens.CreateTokenAsync(user, cancellationToken);
        return Ok(new LoginResponse(token));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email, EmailConfirmed = true };
        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _users.AddToRoleAsync(user, Roles.User);
        var token = await _tokens.CreateTokenAsync(user, cancellationToken);
        return Ok(new LoginResponse(token));
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken);
