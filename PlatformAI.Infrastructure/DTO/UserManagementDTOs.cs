namespace PlatformAI.Infrastructure.DTO;

// ── View Models (risposte API) ────────────────────────────────────────────────

/// <summary>
/// Rappresentazione completa di un utente per la pagina di gestione admin,
/// include il ruolo e il tenant risolti via Include.
/// </summary>
public class UserAdminVM
{
    public Guid    Id           { get; set; }
    public string  Name         { get; set; } = string.Empty;
    public string  Surname      { get; set; } = string.Empty;
    public string  Login        { get; set; } = string.Empty;
    public string? Email        { get; set; }
    public string? MobilePhone  { get; set; }
    public bool    Enabled      { get; set; }
    public Guid    RoleId       { get; set; }
    public RoleAdminVM?   Role   { get; set; }
    public Guid    TenantId     { get; set; }
    public TenantAdminVM? Tenant { get; set; }
    public string  LanguageCode { get; set; } = "it";
}

public class RoleAdminVM
{
    public Guid   Id          { get; set; }
    public string Code        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class TenantAdminVM
{
    public Guid   Id   { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class CreateUserAdminRequest
{
    public string  Name         { get; set; } = string.Empty;
    public string  Surname      { get; set; } = string.Empty;
    public string  Login        { get; set; } = string.Empty;
    public string? Email        { get; set; }
    public string? MobilePhone  { get; set; }
    public string  LanguageCode { get; set; } = "it";
    public Guid    RoleId       { get; set; }
    public bool    Enabled      { get; set; } = true;
    /// <summary>Password in chiaro — verrà hashata prima del salvataggio.</summary>
    public string  Password     { get; set; } = string.Empty;
}

public class UpdateUserAdminRequest
{
    public string  Name         { get; set; } = string.Empty;
    public string  Surname      { get; set; } = string.Empty;
    public string  Login        { get; set; } = string.Empty;
    public string? Email        { get; set; }
    public string? MobilePhone  { get; set; }
    public string  LanguageCode { get; set; } = "it";
    public Guid    RoleId       { get; set; }
    public bool    Enabled      { get; set; }
    /// <summary>
    /// Nuova password in chiaro. Se null o vuota la password NON viene modificata.
    /// </summary>
    public string? Password     { get; set; }
}

public class ToggleEnabledRequest
{
    public bool Enabled { get; set; }
}
