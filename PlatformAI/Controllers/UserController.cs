using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformAI.Core.Services;
using PlatformAI.Core.Logic;
using PlatformAI.Infrastructure.DTO;


namespace PlatformAI.Api.Controllers;

[ApiController]
[Route("api/common/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly UserLogic _userLogic;

    public UserController(IAuthService authService, UserLogic userLogic)
    {
        _authService = authService;
        _userLogic = userLogic;
    }

    /// <summary>
    /// Get current user data
    /// </summary>
    [HttpGet]
    public IActionResult GetUser(Guid userId)
    {
        // var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        //     ?? User.FindFirst("sub")?.Value;
        
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var user = _userLogic.GetUserById(userId);
        
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Update current user data
    /// </summary>
    [HttpPatch]
    public IActionResult UpdateUser([FromBody] UpdateUserRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // In production, update in database
        // For now, return the updated user from request
        return Ok(request.User);
    }
}

public record UpdateUserRequest(UserDTO User);
