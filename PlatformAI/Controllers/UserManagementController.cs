using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Core.Logic;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.DTO;
using PlatformAI.Infrastructure.Master;

namespace PlatformAI.Controllers;

/// <summary>
/// Gestione completa degli utenti (lista, crea, modifica, elimina, abilita/disabilita).
/// Tutti gli endpoint operano sul tenant dell'utente autenticato (isolamento multi-tenant).
/// </summary>
[Route("api/admin")]
[ApiController]
[Authorize]
public class UserManagementController : ControllerBase
{
    private readonly UserLogic                    _userLogic;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(
        UserLogic userLogic,
        ILogger<UserManagementController> logger)
    {
        _userLogic = userLogic;
        _logger    = logger;
    }

    // ── GET /api/admin/users ───────────────────────────────────────────────────
    // Restituisce tutti gli utenti del tenant corrente, con Role e Tenant inclusi.

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserAdminVM>>> GetUsers()
    {
        var requestingUserId = GetCurrentUserId();
        if (requestingUserId == Guid.Empty)
            return Unauthorized("Token non valido o utente non identificabile.");

        try
        {
            _logger.LogInformation("Richiesta lista utenti — requestingUserId={UserId}", requestingUserId);
            var users = await _userLogic.GetAllUsersAdminAsync(requestingUserId);
            return Ok(users);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "GetUsers fallito");
            return NotFound(new { message = ex.Message });
        }
    }

    // ── GET /api/admin/roles ───────────────────────────────────────────────────
    // Restituisce tutti i ruoli disponibili (UserRole).

    [HttpGet("roles")]
    public async Task<ActionResult<IEnumerable<RoleAdminVM>>> GetRoles()
    {
        var roles = await _userLogic.GetAllRolesAsync();
        return Ok(roles);
    }

    // ── POST /api/admin/users ──────────────────────────────────────────────────
    // Crea un nuovo utente nello stesso tenant del richiedente.

    [HttpPost("users")]
    public async Task<ActionResult<UserAdminVM>> CreateUser(
        [FromBody] CreateUserAdminRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var requestingUserId = GetCurrentUserId();
        if (requestingUserId == Guid.Empty)
            return Unauthorized("Token non valido o utente non identificabile.");

        try
        {
            _logger.LogInformation(
                "Creazione utente — login={Login} requestingUserId={RequestingUserId}",
                request.Login, requestingUserId);

            var created = await _userLogic.CreateUserAdminAsync(request, requestingUserId);

            _logger.LogInformation("Utente creato — id={UserId} login={Login}", created.Id, created.Login);
            return CreatedAtAction(nameof(GetUser), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "CreateUser fallito — {Message}", ex.Message);
            return Conflict(new { message = ex.Message });
        }
    }

    // ── GET /api/admin/users/{id} ──────────────────────────────────────────────
    // Restituisce un singolo utente per id (usato come location header dopo la POST).

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<UserAdminVM>> GetUser(Guid id)
    {
        var requestingUserId = GetCurrentUserId();
        if (requestingUserId == Guid.Empty)
            return Unauthorized();

        // Recupera tutti gli utenti del tenant e filtra — riusa la logica di isolamento
        var users = await _userLogic.GetAllUsersAdminAsync(requestingUserId);
        var user  = users.FirstOrDefault(u => u.Id == id);

        if (user is null)
            return NotFound(new { message = "Utente non trovato." });

        return Ok(user);
    }

    // ── PUT /api/admin/users/{id} ──────────────────────────────────────────────
    // Aggiorna i dati anagrafici e il ruolo di un utente esistente.

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<UserAdminVM>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserAdminRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            _logger.LogInformation("Aggiornamento utente — id={UserId}", id);

            var updated = await _userLogic.UpdateUserAdminAsync(id, request);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "UpdateUser fallito — {Message}", ex.Message);

            // "non trovato" → 404, unicità violata → 409
            return ex.Message.Contains("non trovato", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { message = ex.Message })
                : Conflict(new { message = ex.Message });
        }
    }

    // ── DELETE /api/admin/users/{id} ───────────────────────────────────────────
    // Elimina definitivamente un utente.

    [HttpDelete("users/{id:guid}")]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        try
        {
            _logger.LogInformation("Eliminazione utente — id={UserId}", id);
            await _userLogic.DeleteUserAdminAsync(id);
            return NoContent(); // 204
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "DeleteUser fallito — {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }

    // ── PATCH /api/admin/users/{id}/toggle-enabled ─────────────────────────────
    // Abilita o disabilita l'account di un utente senza toccare altri campi.

    [HttpPatch("users/{id:guid}/toggle-enabled")]
    public async Task<ActionResult> ToggleEnabled(
        Guid id,
        [FromBody] ToggleEnabledRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Toggle enabled utente — id={UserId} enabled={Enabled}", id, request.Enabled);

            await _userLogic.ToggleUserEnabledAsync(id, request.Enabled);
            return NoContent(); // 204
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ToggleEnabled fallito — {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Estrae il Guid dell'utente autenticato dal claim JWT "sub"
    /// (uguale a JwtRegisteredClaimNames.Sub).
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
