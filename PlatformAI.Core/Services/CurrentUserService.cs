using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Master;

namespace PlatformAI.Core.Services;

/// <summary>
/// Implementazione di ICurrentUserService che legge l'utente corrente
/// direttamente dall'HttpContext (claims JWT), evitando dipendenze circolari.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MasterContext _masterContext;
    private User? _cachedUser;
    private bool _userLoaded;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, MasterContext masterContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _masterContext = masterContext;
    }

    public string? GetCurrentUserEmail()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
    }

    public User? GetCurrentUser()
    {
        // Lazy loading con cache per evitare query multiple nella stessa request
        try
        {
            if (_userLoaded)
                return _cachedUser;

            _userLoaded = true;

            var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                return null;

            // Carica l'utente direttamente dal MasterContext (bypassa UnitOfWork per evitare ciclo)
            _cachedUser = _masterContext.Users.FirstOrDefault(u => u.Id == userId);
            return _cachedUser;
        }
        catch (Exception ex)
        {
            // Log dell'eccezione se necessario
            Console.WriteLine($"Errore in GetCurrentUser: {ex.Message}");
            return null;
        }

    }
}
