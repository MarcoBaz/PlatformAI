using PlatformAI.Infrastructure.Master;

namespace PlatformAI.Infrastructure;

/// <summary>
/// Interfaccia per ottenere le informazioni dell'utente corrente.
/// Questa interfaccia è definita in Infrastructure per evitare dipendenze circolari
/// e deve essere implementata nel progetto principale o in Core.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Ottiene l'email dell'utente corrente autenticato.
    /// </summary>
    /// <returns>L'email dell'utente o null se non autenticato</returns>
    string? GetCurrentUserEmail();
    
    /// <summary>
    /// Ottiene l'ID dell'utente corrente autenticato.
    /// </summary>
    /// <returns>L'ID dell'utente o null se non autenticato</returns>
    User? GetCurrentUser();
}
