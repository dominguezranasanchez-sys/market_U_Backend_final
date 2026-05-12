using MercadoU.Api.DTOs;

namespace MercadoU.Api.Application.Interfaces;

public interface IProductRepository
{
    Task<IEnumerable<ProductDto>> SearchAsync(ProductSearchParams p);
    Task<ProductDto?> GetByIdAsync(int id);
    Task<IEnumerable<ProductDto>> GetBySellerAsync(int sellerId);
    Task<int> CreateAsync(CreateProductRequest req);
    Task<IEnumerable<ProductImageDto>> GetImagesAsync(int productId);
    Task<ProductImageDto> AddImageAsync(int productId, AddImageRequest req);
}

public interface ICategoryRepository
{
    Task<IEnumerable<CategoryDto>> GetAllAsync();
}

public interface ILocationRepository
{
    Task<IEnumerable<LocationDto>> GetAllAsync();
}

public interface IUniversityRepository
{
    Task<IEnumerable<UniversityDto>> GetAllAsync(int? locationId);
}

public interface IUserRepository
{
    Task<UserDto?> GetByIdAsync(int id);
    Task<(string PasswordHash, UserDto User)?> GetByEmailAsync(string email);
    Task<int> CreateAsync(RegisterRequest req, string passwordHash);
    Task<IEnumerable<ReviewDto>> GetReviewsAsync(int userId);
}

public interface IFavoriteRepository
{
    Task<IEnumerable<ProductDto>> GetByUserAsync(int userId);
    Task<bool> ToggleAsync(int userId, int productId);
}

public interface IConversationRepository
{
    Task<IEnumerable<ConversationDto>> GetByUserAsync(int userId);
    Task<ConversationDto> GetOrCreateAsync(StartConversationRequest req);
}

public interface IMessageRepository
{
    Task<IEnumerable<MessageDto>> GetByConversationAsync(int conversationId);
    Task<MessageDto> SendAsync(int conversationId, SendMessageRequest req);
}
