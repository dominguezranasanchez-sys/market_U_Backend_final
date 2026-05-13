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
// GET  /api/users/favorites           ← autenticado via JWT
// POST /api/users/favorites/{productId} ← autenticado via JWT (toggle)
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
    // GET /api/users/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await userRepo.GetByIdAsync(id);
        if (user is null) return NotFound(new { message = "Usuario no encontrado." });
        return Ok(user);
    }

    // GET /api/users/{id}/products
    [HttpGet("{id:int}/products")]
    public async Task<IActionResult> GetProducts(int id)
    {
        var products = await productRepo.GetBySellerAsync(id);
        return Ok(products);
    }

    // GET /api/users/{id}/reviews
    // FIX: devuelve ReviewDto sin campo CreatedAt para que coincida con lo que espera el frontend.
    [HttpGet("{id:int}/reviews")]
    public async Task<IActionResult> GetReviews(int id)
    {
        var reviews = await userRepo.GetReviewsAsync(id);
        return Ok(reviews);
    }

    // ---------------------------------------------------------------
    // GET /api/users/favorites
    // FIX: endpoint protegido que usa el userId del JWT en lugar de
    //      requerir {id} en la URL — evita que un usuario pida
    //      favoritos de otro usuario.
    // NOTA: Esta ruta literal DEBE estar ANTES de "{id:int}/..." para
    //       que el router no intente parsear "favorites" como int.
    // ---------------------------------------------------------------
    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> GetMyFavorites()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Token inválido." });

        var favs = await favRepo.GetByUserAsync(userId.Value);
        return Ok(favs);
    }

    // ---------------------------------------------------------------
    // POST /api/users/favorites/{productId}
    // FIX: protegido por JWT; usa el userId del token (no de la URL)
    //      para evitar que alguien manipule favoritos de otro usuario.
    //      Devuelve { "active": true/false }.
    // ---------------------------------------------------------------
    [HttpPost("favorites/{productId:int}")]
    [Authorize]
    public async Task<IActionResult> ToggleMyFavorite(int productId)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Token inválido." });

        var active = await favRepo.ToggleAsync(userId.Value, productId);
        return Ok(new FavoriteToggleResponse(active));
    }

    // GET /api/users/{id}/conversations
    [HttpGet("{id:int}/conversations")]
    [Authorize]
    public async Task<IActionResult> GetConversations(int id)
    {
        var conversations = await convRepo.GetByUserAsync(id);
        return Ok(conversations);
    }

    // ---------------------------------------------------------------
    // Helper: extrae el userId del claim "sub" del JWT
    // ---------------------------------------------------------------
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
// POST /api/conversations            → crea o devuelve conversación existente
// GET  /api/conversations/{id}/messages
// POST /api/conversations/{id}/messages
// ======================================================================
[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(
    IConversationRepository convRepo,
    IMessageRepository msgRepo) : ControllerBase
{
    // POST /api/conversations
    // FIX: el frontend manda { sellerId, productId } sin buyerId.
    //      El buyerId se extrae del JWT (usuario autenticado = comprador).
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Start([FromBody] StartConversationFromFrontendRequest req)
    {
        var buyerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

        if (!int.TryParse(buyerIdClaim, out var buyerId))
            return Unauthorized(new { message = "Token inválido o expirado." });

        var fullReq = new StartConversationRequest(buyerId, req.SellerId, req.ProductId);
        var conversation = await convRepo.GetOrCreateAsync(fullReq);
        return Ok(conversation);
    }

    // GET /api/conversations/{id}/messages
    [HttpGet("{id:int}/messages")]
    [Authorize]
    public async Task<IActionResult> GetMessages(int id)
    {
        var messages = await msgRepo.GetByConversationAsync(id);
        return Ok(messages);
    }

    // POST /api/conversations/{id}/messages
    // Body: { senderId: number, content: string }
    [HttpPost("{id:int}/messages")]
    [Authorize]
    public async Task<IActionResult> Send(int id, [FromBody] SendMessageRequest req)
    {
        var message = await msgRepo.SendAsync(id, req);
        return Ok(message);
    }
}
