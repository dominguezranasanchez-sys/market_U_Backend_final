namespace MercadoU.Api.DTOs;

// ============================================================
// Auth
// ============================================================

public record LoginRequest(string Email, string Password);

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    int LocationId,
    int? UniversityId);

public record AuthResponse(UserDto User, string Token);

// ============================================================
// Users
// ============================================================

public record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    int? LocationId,
    int? UniversityId,
    decimal AverageRating,
    string? AvatarUrl);   // maps from ProfilePictureUrl

public record ReviewDto(
    int Id,
    int AuthorId,
    int ReviewedUserId,
    int Rating,
    string? Comment,
    DateTime CreatedAt);

// ============================================================
// Products  — shape that the React frontend expects
// ============================================================

/// <summary>
/// DTO principal de producto.  El frontend (mock.ts) espera estos campos en camelCase.
/// La condición se normaliza: BD usa 'New'/'LikeNew'/'Good'/'Fair'/'Poor'
///   → frontend usa 'new'/'used' (simplificado).
/// </summary>
public record ProductDto(
    int Id,
    string Title,
    string Description,
    decimal Price,
    bool NegotiablePrice,
    int SellerId,
    int CategoryId,
    int? LocationId,
    int? UniversityId,     // optional; not stored in Products but passed through
    string Condition,      // raw DB value: New | LikeNew | Good | Fair | Poor
    string Status,         // active | sold | paused  (lowercased for FE)
    int ViewCount,
    int FavoriteCount,
    string? PrimaryImageUrl,
    string CreatedAt);     // ISO-8601 string for JSON serialization simplicity

public record ProductImageDto(
    int ProductId,
    string Url,
    bool IsPrimary,
    int DisplayOrder);

public record CreateProductRequest(
    string Title,
    string Description,
    decimal Price,
    bool NegotiablePrice,
    int CategoryId,
    int LocationId,
    int? UniversityId,
    int SellerId,
    string Condition);     // "new" | "used" from frontend

public record AddImageRequest(string Url, bool IsPrimary);

// ============================================================
// Product Search  — maps to sp_SearchProducts
// ============================================================

public record ProductSearchParams(
    string? Q = null,
    int? CategoryId = null,
    int? LocationId = null,
    int? UniversityId = null,   // used to filter by seller's university (join)
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Condition = null,
    string SortBy = "Recent",
    int PageSize = 20,
    int? LastId = null,
    string? LastValue = null);

// ============================================================
// Categories / Locations / Universities
// ============================================================

public record CategoryDto(
    int Id,
    string Name,
    string Slug,
    int? ParentCategoryId,
    int DisplayOrder);

public record LocationDto(
    int Id,
    string Country,
    string State,
    string City,
    string? ZipCode);

public record UniversityDto(
    int Id,
    string Name,
    string? Acronym,
    int? LocationId);

// ============================================================
// Conversations & Messages
// ============================================================

public record ConversationDto(
    int Id,
    int BuyerId,
    int SellerId,
    int ProductId,
    DateTime? LastMessageAt);

public record StartConversationRequest(int BuyerId, int SellerId, int ProductId);

public record MessageDto(
    int Id,
    int ConversationId,
    int SenderId,
    string Content,
    string MessageType,
    bool IsRead,
    string SentAt);    // ISO-8601

public record SendMessageRequest(int SenderId, string Content);

// ============================================================
// Favorites
// ============================================================

public record FavoriteToggleResponse(bool Active);
