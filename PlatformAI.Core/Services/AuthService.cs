using Microsoft.Extensions.Logging;
using PlatformAI.Core.Logic;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.DTO;
using PlatformAI.Infrastructure.Master;

namespace PlatformAI.Core.Services;
    
public interface IAuthService
{
    Task<AuthResponse?> SignInAsync(SignInRequest request);
    Task<AuthResponse?> SignInWithTokenAsync(string accessToken);
    Task<bool> SignUpAsync(SignUpRequest request);
    Task<bool> ForgotPasswordAsync(string email);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
    Task<AuthResponse?> UnlockSessionAsync(UnlockSessionRequest request);
    User CurrentUser { get; }
}

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthService> _logger;

    private UserLogic  _userLogic;
    private User  _currentUser;


    public User CurrentUser => _currentUser;
    public AuthService(IJwtService jwtService,UserLogic  userLogic, ILogger<AuthService> logger)
    {
        _jwtService = jwtService;
        _logger = logger;
        _userLogic = userLogic;
    }

    public Task<AuthResponse?> SignInAsync(SignInRequest request)
    {
        _logger.LogInformation("Sign in attempt for {Email}", request.Email);

        var user = _userLogic.GetUserByEmail(request.Email);
        if (user == null)
        {
            _logger.LogWarning("User not found: {Email}", request.Email);
            return Task.FromResult<AuthResponse?>(null);
        }
        _currentUser = user;
        var userData = InfrastructureUtil.MapperManager.Map<User, UserDTO>(user);
        if (!VerifyPassword(request.Password, user.Password))
        {
            _logger.LogWarning("Invalid password for: {Email}", request.Email);
            return Task.FromResult<AuthResponse?>(null);
        }

        var accessToken = _jwtService.GenerateAccessToken(userData);
        
        _logger.LogInformation("User {Email} signed in successfully", request.Email);
        
        return Task.FromResult<AuthResponse?>(new AuthResponse(
            User: userData,
            AccessToken: accessToken
        ));
    }

    public Task<AuthResponse?> SignInWithTokenAsync(string accessToken)
    {
         _logger.LogInformation("Entering SignInWithTokenAsync");
        var userIdString = _jwtService.GetUserIdFromToken(accessToken);
        
        if (userIdString == null)
        {
            _logger.LogWarning("Invalid token provided for sign-in with token");
            return Task.FromResult<AuthResponse?>(null);
        }
        var userId= Guid.Parse(userIdString);
        var user = _userLogic.GetUserById(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found for token: {UserId}", userId);
            return Task.FromResult<AuthResponse?>(null);
        }
        _currentUser = user;
        var userData = InfrastructureUtil.MapperManager.Map<User, UserDTO>(user);
        // Generate new token (token refresh)
        var newAccessToken = _jwtService.GenerateAccessToken(userData);
        
        return Task.FromResult<AuthResponse?>(new AuthResponse(
            User: userData,
            AccessToken: newAccessToken
        ));
    }

    public Task<bool> SignUpAsync(SignUpRequest request)
    {
        var email = request.Email.ToLowerInvariant();
        
        // if (_users.ContainsKey(email))
        // {
        //     _logger.LogWarning("Signup failed - email already exists: {Email}", email);
        //     return Task.FromResult(false);
        // }

        // var newUser = new UserDto(
        //     Id: Guid.NewGuid().ToString(),
        //     Name: request.Name,
        //     Email: email,
        //     Status: "online"
        // );

        // _users[email] = (HashPassword(request.Password), newUser);
        
        _logger.LogInformation("User {Email} registered successfully", email);
        return Task.FromResult(true);
    }

    public Task<bool> ForgotPasswordAsync(string email)
    {
        // In production: generate reset token, save to DB, send email
        _logger.LogInformation("Password reset requested for {Email}", email);
        return Task.FromResult(true); //Task.FromResult(_users.ContainsKey(email.ToLowerInvariant()));
    }

    public Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        // In production: validate reset token, update password
        _logger.LogInformation("Password reset completed");
        return Task.FromResult(true);
    }

    public Task<AuthResponse?> UnlockSessionAsync(UnlockSessionRequest request)
    {
        // Same logic as SignIn for session unlock
        return SignInAsync(new SignInRequest(request.Email, request.Password));
    }

   

    // Simple password hashing for demo - use BCrypt or Argon2 in production
    private static string HashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password + "PlatformAI_Salt_2024");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var passwd = HashPassword(password);
        return passwd == hash;
    }
}
