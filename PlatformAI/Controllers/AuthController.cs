using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PlatformAI.Core.Logic;
using PlatformAI.Core.Services;
using PlatformAI.Infrastructure.DTO;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PlatFormAI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly UserLogic _userLogic;

    public AuthController(IAuthService authService,UserLogic  userLogic,  ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
        _userLogic = userLogic;
    }

    /// <summary>
    /// Sign in with email and password
    /// </summary>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request)
    {
        var result = await _authService.SignInAsync(request);
        
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Sign in using existing access token (token refresh)
    /// </summary>
    [HttpPost("sign-in-with-token")]
    [AllowAnonymous]
    public async Task<IActionResult> SignInWithToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.SignInWithTokenAsync(request.AccessToken);
        
        if (result == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("sign-up")]
    [AllowAnonymous]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        var success = await _authService.SignUpAsync(request);
        
        if (!success)
        {
            return BadRequest(new { message = "Email already exists" });
        }

        return Ok(new { message = "Registration successful" });
    }

    /// <summary>
    /// Request password reset
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request.Email);
        // Always return success to prevent email enumeration
        return Ok(new { message = "If your email exists, you will receive a reset link" });
    }

    /// <summary>
    /// Reset password with token
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var success = await _authService.ResetPasswordAsync(request);
        
        if (!success)
        {
            return BadRequest(new { message = "Invalid or expired reset token" });
        }

        return Ok(new { message = "Password reset successful" });
    }

    /// <summary>
    /// Unlock session for locked users
    /// </summary>
    [HttpPost("unlock-session")]
    [AllowAnonymous]
    public async Task<IActionResult> UnlockSession([FromBody] UnlockSessionRequest request)
    {
        var result = await _authService.UnlockSessionAsync(request);
        
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get current user info (requires authentication)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
         string userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        Guid userId = Guid.Parse(userIdString??Guid.Empty.ToString());
        
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var user = _userLogic.GetUserById(userId);
        
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(user);
    }
}