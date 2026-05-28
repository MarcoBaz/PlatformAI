using System.ComponentModel.DataAnnotations;

namespace PlatformAI.Infrastructure.DTO;

// ========== Request DTOs ==========

public record SignInRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false
);

public record SignUpRequest(
    [Required] string Name,
    [Required][EmailAddress] string Email,
    [Required][MinLength(6)] string Password,
    string? Company = null
);

public record RefreshTokenRequest(
    [Required] string AccessToken
);

public record ForgotPasswordRequest(
    [Required][EmailAddress] string Email
);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required][MinLength(6)] string Password
);

public record UnlockSessionRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

// ========== Response DTOs ==========

public record AuthResponse(
    UserDTO User,
    string AccessToken,
    string TokenType = "bearer"
);


