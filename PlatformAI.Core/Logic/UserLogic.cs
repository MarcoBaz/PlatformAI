using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.DTO;
using PlatformAI.Infrastructure.Master;

namespace PlatformAI.Core.Logic;

public class UserLogic
{
    private readonly IUnitOfWork       _uow;
    private readonly IServiceProvider  _serviceProvider;
    private readonly IRepository<User> _userRepository;

    public UserLogic(IUnitOfWork uow, IServiceProvider serviceProvider)
    {
        _uow             = uow;
        _serviceProvider = serviceProvider;
        _userRepository  = _uow.Repository<User>();
    }

    // ── Metodi originali (invariati) ──────────────────────────────────────────

    public async Task<List<User>> GetAllUsers(string tenantCode)
    {
        return await _userRepository
            .Query(x => x.Tenant.Code == tenantCode)
            .Include(x => x.Tenant)
            .ToListAsync();
    }

    public async Task<User> SaveUserAsync(UserDTO userDTO)
    {
        var user = InfrastructureUtil.MapperManager.Map<UserDTO, User>(userDTO);
        var existingUser = await _userRepository
            .Query(x => x.Email == userDTO.Email)
            .FirstOrDefaultAsync();

        if (existingUser != null)
            await _userRepository.AddAsync(user);
        else
            await _userRepository.UpdateAsync(user);

        await _uow.SaveChangesAsync();
        return user;
    }

    public User? GetUserById(Guid userId)
        => _userRepository.Query(x => x.Id == userId).FirstOrDefault();

    public User? GetUserByEmail(string email)
        => _userRepository.Query(x => x.Email == email).FirstOrDefault();

    // ── Metodi Admin (gestione utenti) ────────────────────────────────────────

    /// <summary>
    /// Restituisce tutti gli utenti dello stesso tenant dell'utente richiedente,
    /// con Role e Tenant risolti via Include.
    /// </summary>
    public async Task<List<UserAdminVM>> GetAllUsersAdminAsync(Guid requestingUserId)
    {
        // Recupera il tenant del richiedente
        var requestingUser = await _userRepository
            .Query(x => x.Id == requestingUserId)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Utente richiedente non trovato.");

        var tenantId = requestingUser.TenantId;

        var users = await _userRepository
            .Query(x => x.TenantId == tenantId)
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .OrderBy(x => x.Surname)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return users.Select(MapToAdminVM).ToList();
    }

    /// <summary>
    /// Restituisce tutti i ruoli disponibili (UserRole : BaseEntity).
    /// Poiché UserRole non estende Entity non è gestito da IRepository,
    /// quindi si accede direttamente a MasterContext tramite IServiceProvider.
    /// </summary>
    public async Task<List<RoleAdminVM>> GetAllRolesAsync()
    {
        var context = _serviceProvider.GetRequiredService<MasterContext>();

        var roles = await context.Set<UserRole>()
            .OrderBy(r => r.Description)
            .ToListAsync();

        return roles.Select(r => new RoleAdminVM
        {
            Id          = r.Id,
            Code        = r.Code,
            Description = r.Description ?? r.Code,
        }).ToList();
    }

    /// <summary>
    /// Crea un nuovo utente nello stesso tenant dell'utente richiedente.
    /// La password viene hashata con SHA-256 + salt prima del salvataggio.
    /// </summary>
    public async Task<UserAdminVM> CreateUserAdminAsync(
        CreateUserAdminRequest request,
        Guid requestingUserId)
    {
        // Recupera il tenant del richiedente
        var requestingUser = await _userRepository
            .Query(x => x.Id == requestingUserId)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Utente richiedente non trovato.");

        // Verifica unicità login ed email
        var loginExists = await _userRepository
            .Query(x => x.Login == request.Login)
            .AnyAsync();
        if (loginExists)
            throw new InvalidOperationException($"Il login '{request.Login}' è già in uso.");

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailExists = await _userRepository
                .Query(x => x.Email == request.Email)
                .AnyAsync();
            if (emailExists)
                throw new InvalidOperationException($"L'email '{request.Email}' è già in uso.");
        }

        var user = new User
        {
            Name         = request.Name.Trim(),
            Surname      = request.Surname.Trim(),
            Login        = request.Login.Trim(),
            Email        = request.Email?.Trim(),
            MobilePhone  = request.MobilePhone?.Trim(),
            LanguageCode = request.LanguageCode,
            RoleId       = request.RoleId,
            TenantId     = requestingUser.TenantId,
            Enabled      = request.Enabled,
            Password     = HashPassword(request.Password),
        };

        await _userRepository.AddAsync(user);
        await _uow.SaveChangesAsync();

        // Ricarica con navigazioni per costruire la VM
        var created = await _userRepository
            .Query(x => x.Id == user.Id)
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .FirstAsync();

        return MapToAdminVM(created);
    }

    /// <summary>
    /// Aggiorna un utente esistente (esclusa la password).
    /// </summary>
    public async Task<UserAdminVM> UpdateUserAdminAsync(
        Guid userId,
        UpdateUserAdminRequest request)
    {
        var user = await _userRepository
            .Query(x => x.Id == userId)
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Utente non trovato.");

        // Verifica unicità login se cambiato
        if (!string.Equals(user.Login, request.Login, StringComparison.OrdinalIgnoreCase))
        {
            var loginExists = await _userRepository
                .Query(x => x.Login == request.Login && x.Id != userId)
                .AnyAsync();
            if (loginExists)
                throw new InvalidOperationException($"Il login '{request.Login}' è già in uso.");
        }

        // Verifica unicità email se cambiata
        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _userRepository
                .Query(x => x.Email == request.Email && x.Id != userId)
                .AnyAsync();
            if (emailExists)
                throw new InvalidOperationException($"L'email '{request.Email}' è già in uso.");
        }

        user.Name         = request.Name.Trim();
        user.Surname      = request.Surname.Trim();
        user.Login        = request.Login.Trim();
        user.Email        = request.Email?.Trim();
        user.MobilePhone  = request.MobilePhone?.Trim();
        user.LanguageCode = request.LanguageCode;
        user.RoleId       = request.RoleId;
        user.Enabled      = request.Enabled;

        // Aggiorna la password solo se valorizzata (minLength validato dal controller/UI)
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.Password = HashPassword(request.Password);

        await _userRepository.UpdateAsync(user);
        await _uow.SaveChangesAsync();

        // Ricarica per avere le navigazioni aggiornate (Role potrebbe essere cambiato)
        var updated = await _userRepository
            .Query(x => x.Id == userId)
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .FirstAsync();

        return MapToAdminVM(updated);
    }

    /// <summary>
    /// Elimina definitivamente un utente dal database.
    /// </summary>
    public async Task DeleteUserAdminAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Utente non trovato.");

        await _userRepository.DeleteAsync(user);
        await _uow.SaveChangesAsync();
    }

    /// <summary>
    /// Abilita o disabilita l'account di un utente senza modificare altri dati.
    /// </summary>
    public async Task ToggleUserEnabledAsync(Guid userId, bool enabled)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Utente non trovato.");

        user.Enabled = enabled;
        await _userRepository.UpdateAsync(user);
        await _uow.SaveChangesAsync();
    }

    // ── Mapper privato ────────────────────────────────────────────────────────

    private static UserAdminVM MapToAdminVM(User u) => new()
    {
        Id          = u.Id,
        Name        = u.Name,
        Surname     = u.Surname,
        Login       = u.Login,
        Email       = u.Email,
        MobilePhone = u.MobilePhone,
        Enabled     = u.Enabled,
        RoleId      = u.RoleId,
        Role        = u.Role is null ? null : new RoleAdminVM
        {
            Id          = u.Role.Id,
            Code        = u.Role.Code,
            Description = u.Role.Description ?? u.Role.Code,
        },
        TenantId    = u.TenantId,
        Tenant      = u.Tenant is null ? null : new TenantAdminVM
        {
            Id   = u.Tenant.Id,
            Code = u.Tenant.Code,
            Name = u.Tenant.Name,
        },
        LanguageCode = u.LanguageCode,
    };

    // ── Helpers password ──────────────────────────────────────────────────────

    /// <summary>
    /// Hashing SHA-256 con salt fisso — allineato all'implementazione di AuthService.
    /// In produzione sostituire con BCrypt / Argon2.
    /// </summary>
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password + "PlatformAI_Salt_2024");
        var hash  = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
