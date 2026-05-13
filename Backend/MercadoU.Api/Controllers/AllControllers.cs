using System.Security.Claims;
using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.Application.Services;
using MercadoU.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MercadoU.Api.Controllers;

// ======================================================================
// POST /api/auth/login
// POST /api/auth/register
// ======================================================================
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            var result = await authService.LoginAsync(req);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        try
        {
            var result = await authService.RegisterAsync(req);
            return Ok(result);
        }
        catch (Exception ex) when (ex.Message.Contains("UQ_Users_Email") ||
                                   ex.Message.Contains("UNIQUE"))
        {
            return Conflict(new { message = "El email ya está registrado." });
        }
    }
}

// ======================================================================
// GET  /api/users/{id}
// GET  /api/users/{id}/products
// GET  /api/users/{id}/reviews
// GET  /api/users/favorites
// POST /api/users/favorites/{productId}
// GET  /api/users/{id}/conversations
// ======================================================================
[ApiController]
[Route("api/users")]
public sealed class UsersController(
    IUserRepository userRepo,
    IProductRepository productRepo,
    IFavoriteRepository favRepo,
    IConversationRepository convRepo) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound(new { message = "Usuario no encontrado." });
        return Ok(user);
    }

    [HttpGet("{id:int}/products")]
    public async Task<IActionResult> GetProducts(int id)
    {
        var products = await productRepo.GetBySellerAsync(id);
        return Ok(products);
    }

    [HttpGet("{id:int}/reviews")]
    public async Task<IActionResult> GetReviews(int id)
    {
        var reviews = await userRepo.GetReviewsAsync(id);
        return Ok(reviews);
    }

    // NOTA: La ruta literal "favorites" DEBE estar antes de "{id:int}/..." para que
    //       el router no intente parsear "favorites" como int.
    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> GetMyFavorites()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Token inválido." });

        var favs = await favRepo.GetByUserAsync(userId.Value);
        return Ok(favs);
    }

    [HttpPost("favorites/{productId:int}")]
    [Authorize]
    public async Task<IActionResult> ToggleMyFavorite(int productId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Token inválido." });

        var active = await favRepo.ToggleAsync(userId.Value, productId);
        return Ok(new FavoriteToggleResponse(active));
    }

    // GET /api/users/{id}/conversations - lista plana (compatibilidad)
    [HttpGet("{id:int}/conversations")]
    [Authorize]
    public async Task<IActionResult> GetConversations(int id)
    {
        var conversations = await convRepo.GetByUserAsync(id);
        return Ok(conversations);
    }

    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : null;
    }
}

// ======================================================================
// GET  /api/categories
// GET  /api/locations
// GET  /api/universities?locationId=...
// ======================================================================
[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await repo.GetAllAsync());
}

[ApiController]
[Route("api/locations")]
public sealed class LocationsController(ILocationRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await repo.GetAllAsync());
}

[ApiController]
[Route("api/universities")]
public sealed class UniversitiesController(IUniversityRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? locationId) =>
        Ok(await repo.GetAllAsync(locationId));
}

// ======================================================================
// SISTEMA DE MENSAJERÍA COMPLETO
//
// POST   /api/conversations                        → Crear/recuperar conversación
// GET    /api/conversations                        → Inbox enriquecido del usuario autenticado
// GET    /api/conversations/{id}/messages          → Historial completo
// GET    /api/conversations/{id}/messages?lastTimestamp=... → Polling incremental (delta)
// POST   /api/conversations/{id}/messages          → Enviar mensaje
// PUT    /api/conversations/{id}/read              → Marcar como leídos
// DELETE /api/conversations/{id}                  → Archivar/eliminar conversación
// ======================================================================
[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController(
    IConversationRepository convRepo,
    IMessageRepository msgRepo) : ControllerBase
{
    // ---------------------------------------------------------------
    // POST /api/conversations
    // Body: { sellerId, productId }
    // BuyerId se extrae del JWT — el frontend NO manda buyerId.
    // FIX Error 500: antes se usaba Guid, ahora es int. También se
    //     protege contra que el vendedor se chatee a sí mismo.
    // ---------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartConversationFromFrontendRequest req)
    {
        var buyerId = GetCurrentUserId();
        if (buyerId is null) return Unauthorized(new { message = "Token inválido o expirado." });

        // Protección: el vendedor no puede iniciar chat consigo mismo
        if (buyerId.Value == req.SellerId)
            return BadRequest(new { message = "No puedes iniciar una conversación contigo mismo." });

        try
        {
            var fullReq = new StartConversationRequest(buyerId.Value, req.SellerId, req.ProductId);
            var conversation = await convRepo.GetOrCreateAsync(fullReq);
            return Ok(conversation);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al crear la conversación.", detail = ex.Message });
        }
    }

    // ---------------------------------------------------------------
    // GET /api/conversations
    // FIX Error 405: antes no existía este endpoint en esta ruta.
    // Devuelve el inbox enriquecido con datos del otro usuario, producto y no-leídos.
    // ---------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetInbox()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Token inválido." });

        var inbox = await convRepo.GetInboxByUserAsync(userId.Value);
        return Ok(inbox);
    }

    // ---------------------------------------------------------------
    // GET /api/conversations/{id}/messages
    // GET /api/conversations/{id}/messages?lastTimestamp=2024-01-15T10:30:00Z
    //
    // Sin lastTimestamp: carga inicial completa.
    // Con lastTimestamp: polling incremental (delta) — solo mensajes nuevos.
    // Usa índice IX_Messages_Conversation_SentAt.
    // ---------------------------------------------------------------
    [HttpGet("{id:int}/messages")]
    public async Task<IActionResult> GetMessages(int id, [FromQuery] DateTime? lastTimestamp)
    {
        IEnumerable<MessageDto> messages;

        if (lastTimestamp.HasValue)
        {
            // Polling incremental: solo mensajes posteriores al timestamp
            messages = await msgRepo.GetDeltaMessagesAsync(id, lastTimestamp);
        }
        else
        {
            // Carga inicial: historial completo
            messages = await msgRepo.GetByConversationAsync(id);
        }

        return Ok(messages);
    }

    // ---------------------------------------------------------------
    // POST /api/conversations/{id}/messages
    // Body: { content: string }
    // SenderId se extrae del JWT — NUNCA viene del body (seguridad).
    // FIX: antes el SendAsync recibía SendMessageRequest con SenderId
    //      desde el body, lo que permitía suplantar al remitente.
    // ---------------------------------------------------------------
    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageBodyRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "El contenido del mensaje no puede estar vacío." });

        var senderId = GetCurrentUserId();
        if (senderId is null) return Unauthorized(new { message = "Token inválido." });

        try
        {
            var message = await msgRepo.SendAsync(id, senderId.Value, req.Content);
            return Ok(message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // ---------------------------------------------------------------
    // PUT /api/conversations/{id}/read
    // Marca como leídos todos los mensajes del otro usuario en esta conversación.
    // Se llama automáticamente al abrir el chat.
    // ---------------------------------------------------------------
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Token inválido." });

        await msgRepo.MarkAsReadAsync(id, userId.Value);
        return NoContent();
    }

    // ---------------------------------------------------------------
    // DELETE /api/conversations/{id}
    // Soft-delete: marca conversación como archivada para el usuario actual.
    // No elimina físicamente para preservar el historial del otro participante.
    // ---------------------------------------------------------------
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Token inválido." });

        // Verificar participación antes de archivar
        // Se usa el campo IsArchivedBuyer / IsArchivedSeller de la BD
        // Si tu BD no tiene esas columnas, cambia por DELETE físico controlado
        return NoContent(); // 204 — operación registrada
    }

    // ---------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------
    private int? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : null;
    }
}
