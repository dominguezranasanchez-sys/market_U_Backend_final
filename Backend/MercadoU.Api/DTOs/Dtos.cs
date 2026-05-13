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
// Products
// ============================================================

public record ProductDto(
    int Id,
    string Title,
    string Description,
    decimal Price,
    bool NegotiablePrice,
    int SellerId,
    int CategoryId,
    int? LocationId,
    int? UniversityId,
    string Condition,
    string Status,
    int ViewCount,
    int FavoriteCount,
    string? PrimaryImageUrl,
    string CreatedAt);

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
    string Condition);

public record AddImageRequest(string Url, bool IsPrimary);

// ============================================================
// Product Search
// ============================================================

public record ProductSearchParams(
    string? Q = null,
    int? CategoryId = null,
    int? LocationId = null,
    int? UniversityId = null,
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

/// <summary>
/// DTO enriquecido para el inbox del chat.
/// Incluye datos del otro participante, del producto y el conteo de no leídos.
/// </summary>
public record ConversationInboxDto(
    int Id,
    int BuyerId,
    string BuyerName,
    string? BuyerPicture,
    int SellerId,
    string SellerName,
    string? SellerPicture,
    int ProductId,
    string ProductTitle,
    decimal ProductPrice,
    string? ProductThumbnail,
    DateTime? LastMessageAt,
    string? LastMessageContent,
    int? LastMessageSenderId,
    int UnreadCount);

/// <summary>Request completo (interno): incluye BuyerId resuelto del JWT.</summary>
public record StartConversationRequest(int BuyerId, int SellerId, int ProductId);

/// <summary>Request que manda el frontend — solo sellerId + productId.</summary>
public record StartConversationFromFrontendRequest(int SellerId, int ProductId);

public record MessageDto(
    int Id,
    int ConversationId,
    int SenderId,
    string Content,
    string MessageType,
    bool IsRead,
    string SentAt);    // ISO-8601

// CORREGIDO: solo Content viene del body. SenderId se saca del JWT en el controlador.
public record SendMessageBodyRequest(string Content);

// Mantener por compatibilidad interna si se usa en otro sitio
public record SendMessageRequest(int SenderId, string Content);

// ============================================================
// Favorites
// ============================================================

public record FavoriteToggleResponse(bool Active);
