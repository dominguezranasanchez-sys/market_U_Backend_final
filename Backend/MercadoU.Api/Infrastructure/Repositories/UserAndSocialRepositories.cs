using Dapper;
using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.DTOs;
using MercadoU.Api.Infrastructure.Data;

namespace MercadoU.Api.Infrastructure.Repositories;

// ======================================================================
// UserRepository
// ======================================================================
public sealed class UserRepository(SqlConnectionFactory db) : IUserRepository
{
    public async Task<UserDto?> GetByIdAsync(int id)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, FirstName, LastName, Email,
                   LocationId, UniversityId, AverageRating,
                   ProfilePictureUrl AS AvatarUrl
            FROM Users
            WHERE Id = @Id AND IsDeleted = 0
            """;

        return await conn.QueryFirstOrDefaultAsync<UserDto>(sql, new { Id = id });
    }

    /// <summary>Returns PasswordHash alongside UserDto for auth validation.</summary>
    public async Task<(string PasswordHash, UserDto User)?> GetByEmailAsync(string email)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, FirstName, LastName, Email,
                   LocationId, UniversityId, AverageRating,
                   ProfilePictureUrl AS AvatarUrl,
                   PasswordHash
            FROM Users
            WHERE Email = @Email AND IsDeleted = 0 AND Status = 'Active'
            """;

        var row = await conn.QueryFirstOrDefaultAsync(sql, new { Email = email });
        if (row is null) return null;

        var user = new UserDto(
            Id:            (int)row.Id,
            FirstName:     (string)row.FirstName,
            LastName:      (string)row.LastName,
            Email:         (string)row.Email,
            LocationId:    (int?)row.LocationId,
            UniversityId:  (int?)row.UniversityId,
            AverageRating: (decimal)row.AverageRating,
            AvatarUrl:     (string?)row.AvatarUrl);

        return ((string)row.PasswordHash, user);
    }

    public async Task<int> CreateAsync(RegisterRequest req, string passwordHash)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            INSERT INTO Users
                (FirstName, LastName, Email, PasswordHash, LocationId, UniversityId, Status)
            OUTPUT INSERTED.Id
            VALUES
                (@FirstName, @LastName, @Email, @PasswordHash, @LocationId, @UniversityId, 'Active')
            """;

        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            req.FirstName,
            req.LastName,
            req.Email,
            PasswordHash   = passwordHash,
            req.LocationId,
            UniversityId   = req.UniversityId
        });
    }

    public async Task<IEnumerable<ReviewDto>> GetReviewsAsync(int userId)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, AuthorId, ReviewedUserId, Rating, Comment, CreatedAt
            FROM Reviews
            WHERE ReviewedUserId = @UserId AND IsDeleted = 0
            ORDER BY CreatedAt DESC
            """;

        return await conn.QueryAsync<ReviewDto>(sql, new { UserId = userId });
    }
}

// ======================================================================
// FavoriteRepository
// ======================================================================
public sealed class FavoriteRepository(SqlConnectionFactory db) : IFavoriteRepository
{
    public async Task<IEnumerable<ProductDto>> GetByUserAsync(int userId)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT
                p.Id, p.Title, p.Description, p.Price, p.NegotiablePrice,
                p.SellerId, p.CategoryId, p.LocationId, p.Condition, p.Status,
                p.ViewCount, p.FavoriteCount, p.CreatedAt,
                pi_img.Url AS PrimaryImageUrl
            FROM Favorites f
            INNER JOIN Products p ON p.Id = f.ProductId AND p.IsDeleted = 0
            LEFT JOIN ProductImages pi_img
                ON pi_img.ProductId = p.Id AND pi_img.IsPrimary = 1
            WHERE f.UserId = @UserId
            ORDER BY f.CreatedAt DESC
            """;

        var rows = await conn.QueryAsync(sql, new { UserId = userId });

        return rows.Select(r => new ProductDto(
            Id:              (int)r.Id,
            Title:           (string)r.Title,
            Description:     (string)r.Description,
            Price:           (decimal)r.Price,
            NegotiablePrice: r.NegotiablePrice == true,
            SellerId:        (int)r.SellerId,
            CategoryId:      (int)r.CategoryId,
            LocationId:      (int?)r.LocationId,
            UniversityId:    null,
            Condition:       r.Condition is "New" ? "new" : "used",
            Status:          ((string)r.Status).ToLowerInvariant(),
            ViewCount:       (int)r.ViewCount,
            FavoriteCount:   (int)r.FavoriteCount,
            PrimaryImageUrl: (string?)r.PrimaryImageUrl,
            CreatedAt:       ((DateTime)r.CreatedAt).ToString("o")));
    }

    /// <summary>Toggle favorite. Returns true if now active, false if removed.</summary>
    public async Task<bool> ToggleAsync(int userId, int productId)
    {
        using var conn = await db.CreateAsync();

        var existing = await conn.ExecuteScalarAsync<int?>(
            "SELECT Id FROM Favorites WHERE UserId = @UserId AND ProductId = @ProductId",
            new { UserId = userId, ProductId = productId });

        if (existing.HasValue)
        {
            await conn.ExecuteAsync(
                "DELETE FROM Favorites WHERE Id = @Id",
                new { Id = existing.Value });

            // Decrement counter
            await conn.ExecuteAsync(
                "UPDATE Products SET FavoriteCount = FavoriteCount - 1 WHERE Id = @ProductId AND FavoriteCount > 0",
                new { ProductId = productId });

            return false;
        }

        await conn.ExecuteAsync(
            "INSERT INTO Favorites (UserId, ProductId) VALUES (@UserId, @ProductId)",
            new { UserId = userId, ProductId = productId });

        // Increment counter
        await conn.ExecuteAsync(
            "UPDATE Products SET FavoriteCount = FavoriteCount + 1 WHERE Id = @ProductId",
            new { ProductId = productId });

        return true;
    }
}

// ======================================================================
// ConversationRepository
// ======================================================================
public sealed class ConversationRepository(SqlConnectionFactory db) : IConversationRepository
{
    public async Task<IEnumerable<ConversationDto>> GetByUserAsync(int userId)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, BuyerId, SellerId, ProductId, LastMessageAt
            FROM Conversations
            WHERE (BuyerId = @UserId OR SellerId = @UserId)
            ORDER BY LastMessageAt DESC
            """;

        return await conn.QueryAsync<ConversationDto>(sql, new { UserId = userId });
    }

    public async Task<ConversationDto> GetOrCreateAsync(StartConversationRequest req)
    {
        using var conn = await db.CreateAsync();

        // UQ_Conversations_Unique (BuyerId, SellerId, ProductId) garantiza unicidad en BD
        const string selectSql = """
            SELECT Id, BuyerId, SellerId, ProductId, LastMessageAt
            FROM Conversations
            WHERE BuyerId = @BuyerId AND SellerId = @SellerId AND ProductId = @ProductId
            """;

        var existing = await conn.QueryFirstOrDefaultAsync<ConversationDto>(selectSql, new
        {
            req.BuyerId, req.SellerId, req.ProductId
        });

        if (existing is not null) return existing;

        const string insertSql = """
            INSERT INTO Conversations (BuyerId, SellerId, ProductId)
            OUTPUT INSERTED.Id, INSERTED.BuyerId, INSERTED.SellerId,
                   INSERTED.ProductId, INSERTED.LastMessageAt
            VALUES (@BuyerId, @SellerId, @ProductId)
            """;

        return await conn.QuerySingleAsync<ConversationDto>(insertSql, new
        {
            req.BuyerId, req.SellerId, req.ProductId
        });
    }
}

// ======================================================================
// MessageRepository
// ======================================================================
public sealed class MessageRepository(SqlConnectionFactory db) : IMessageRepository
{
    public async Task<IEnumerable<MessageDto>> GetByConversationAsync(int conversationId)
    {
       using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, ConversationId, SenderId, Content, MessageType, IsRead, SentAt
            FROM Messages
            WHERE ConversationId = @ConversationId AND IsDeleted = 0
            ORDER BY SentAt ASC
            """;

        var rows = await conn.QueryAsync(sql, new { ConversationId = conversationId });

        return rows.Select(r => new MessageDto(
            Id:             (int)r.Id,
            ConversationId: (int)r.ConversationId,
            SenderId:       (int)r.SenderId,
            Content:        (string)r.Content,
            MessageType:    (string)r.MessageType,
            IsRead:         r.IsRead == true,
            SentAt:         ((DateTime)r.SentAt).ToString("o")));
    }

    public async Task<MessageDto> SendAsync(int conversationId, SendMessageRequest req)
    {
     using var conn = await db.CreateAsync();

        const string sql = """
            INSERT INTO Messages (ConversationId, SenderId, Content, MessageType)
            OUTPUT INSERTED.Id, INSERTED.ConversationId, INSERTED.SenderId,
                   INSERTED.Content, INSERTED.MessageType, INSERTED.IsRead, INSERTED.SentAt
            VALUES (@ConversationId, @SenderId, @Content, 'Text')
            """;

        // El trigger trg_Messages_UpdateConversationLastMessage actualiza Conversations.LastMessageAt
        var row = await conn.QuerySingleAsync(sql, new
        {
            ConversationId = conversationId,
            req.SenderId,
            req.Content
        });

        return new MessageDto(
            Id:             (int)row.Id,
            ConversationId: (int)row.ConversationId,
            SenderId:       (int)row.SenderId,
            Content:        (string)row.Content,
            MessageType:    (string)row.MessageType,
            IsRead:         row.IsRead == true,
            SentAt:         ((DateTime)row.SentAt).ToString("o"));
    }
}
