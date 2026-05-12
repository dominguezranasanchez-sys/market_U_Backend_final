using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.Application.Services;
using MercadoU.Api.DTOs;
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
// GET  /api/users/{id}/favorites
// POST /api/users/{userId}/favorites/{productId}
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
    [HttpGet("{id:int}/reviews")]
    public async Task<IActionResult> GetReviews(int id)
    {
        var reviews = await userRepo.GetReviewsAsync(id);
        return Ok(reviews);
    }

    // GET /api/users/{id}/favorites
    [HttpGet("{id:int}/favorites")]
    public async Task<IActionResult> GetFavorites(int id)
    {
        var favs = await favRepo.GetByUserAsync(id);
        return Ok(favs);
    }

    // POST /api/users/{userId}/favorites/{productId}
    [HttpPost("{userId:int}/favorites/{productId:int}")]
    public async Task<IActionResult> ToggleFavorite(int userId, int productId)
    {
        var active = await favRepo.ToggleAsync(userId, productId);
        return Ok(new FavoriteToggleResponse(active));
    }

    // GET /api/users/{id}/conversations
    [HttpGet("{id:int}/conversations")]
    public async Task<IActionResult> GetConversations(int id)
    {
        var conversations = await convRepo.GetByUserAsync(id);
        return Ok(conversations);
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
// POST /api/conversations                        → startWithSeller
// GET  /api/conversations/{id}/messages          → messages(conversationId)
// POST /api/conversations/{id}/messages          → send(...)
// ======================================================================
[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(
    IConversationRepository convRepo,
    IMessageRepository msgRepo) : ControllerBase
{
    // POST /api/conversations
    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartConversationRequest req)
    {
        var conversation = await convRepo.GetOrCreateAsync(req);
        return Ok(conversation);
    }

    // GET /api/conversations/{id}/messages
    [HttpGet("{id:int}/messages")]
    public async Task<IActionResult> GetMessages(int id)
    {
        var messages = await msgRepo.GetByConversationAsync(id);
        return Ok(messages);
    }

    // POST /api/conversations/{id}/messages
    // Body: { senderId: number, content: string }
    // El trigger trg_Messages_UpdateConversationLastMessage actualiza LastMessageAt automáticamente.
    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> Send(int id, [FromBody] SendMessageRequest req)
    {
        var message = await msgRepo.SendAsync(id, req);
        return Ok(message);
    }
}
