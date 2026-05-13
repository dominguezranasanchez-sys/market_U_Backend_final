using Dapper;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
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

            await conn.ExecuteAsync(
                "UPDATE Products SET FavoriteCount = FavoriteCount - 1 WHERE Id = @ProductId AND FavoriteCount > 0",
                new { ProductId = productId });

            return false;
        }

        await conn.ExecuteAsync(
            "INSERT INTO Favorites (UserId, ProductId) VALUES (@UserId, @ProductId)",
            new { UserId = userId, ProductId = productId });

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
    /// <summary>
    /// Inbox enriquecido: nombre del otro usuario, producto, último mensaje, no-leídos.
    /// Usa la vista vw_ConversationInbox ya existente en la BD + subconsulta de unread.
    /// </summary>
    public async Task<IEnumerable<ConversationInboxDto>> GetInboxByUserAsync(int userId)
    {
        using var conn = await db.CreateAsync();

        // Consulta directa sin depender de la vista para mayor control y compatibilidad
        const string sql = """
            SELECT
                c.Id,
                c.BuyerId,
                buyer.FirstName + ' ' + buyer.LastName   AS BuyerName,
                buyer.ProfilePictureUrl                  AS BuyerPicture,
                c.SellerId,
                seller.FirstName + ' ' + seller.LastName AS SellerName,
                seller.ProfilePictureUrl                 AS SellerPicture,
                c.ProductId,
                p.Title                                  AS ProductTitle,
                p.Price                                  AS ProductPrice,
                pi_img.Url                               AS ProductThumbnail,
                c.LastMessageAt,
                last_msg.Content                         AS LastMessageContent,
                last_msg.SenderId                        AS LastMessageSenderId,
                ISNULL((
                    SELECT COUNT(*)
                    FROM Messages m
                    WHERE m.ConversationId = c.Id
                      AND m.IsRead = 0
                      AND m.SenderId <> @UserId
                      AND m.IsDeleted = 0
                ), 0)                                    AS UnreadCount
            FROM Conversations c
            INNER JOIN Users buyer  ON c.BuyerId  = buyer.Id
            INNER JOIN Users seller ON c.SellerId = seller.Id
            INNER JOIN Products p   ON c.ProductId = p.Id
            LEFT  JOIN ProductImages pi_img
                ON pi_img.ProductId = p.Id AND pi_img.IsPrimary = 1
            OUTER APPLY (
                SELECT TOP 1 Content, SenderId
                FROM Messages m2
                WHERE m2.ConversationId = c.Id AND m2.IsDeleted = 0
                ORDER BY m2.SentAt DESC
            ) last_msg
            WHERE c.BuyerId = @UserId OR c.SellerId = @UserId
            ORDER BY c.LastMessageAt DESC
            """;

        var rows = await conn.QueryAsync(sql, new { UserId = userId });

        return rows.Select(r => new ConversationInboxDto(
            Id:                   (int)r.Id,
            BuyerId:              (int)r.BuyerId,
            BuyerName:            (string)r.BuyerName,
            BuyerPicture:         (string?)r.BuyerPicture,
            SellerId:             (int)r.SellerId,
            SellerName:           (string)r.SellerName,
            SellerPicture:        (string?)r.SellerPicture,
            ProductId:            (int)r.ProductId,
            ProductTitle:         (string)r.ProductTitle,
            ProductPrice:         (decimal)r.ProductPrice,
            ProductThumbnail:     (string?)r.ProductThumbnail,
            LastMessageAt:        (DateTime?)r.LastMessageAt,
            LastMessageContent:   (string?)r.LastMessageContent,
            LastMessageSenderId:  (int?)r.LastMessageSenderId,
            UnreadCount:          (int)r.UnreadCount));
    }

    /// <summary>Lista plana de conversaciones (para UsersController).</summary>
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

        // Si el usuario intenta chatear consigo mismo, rechazar
        if (req.BuyerId == req.SellerId)
            throw new InvalidOperationException("Un usuario no puede iniciar una conversación consigo mismo.");

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

        return rows.Select(MapRow);
    }

    /// <summary>
    /// Polling incremental: devuelve solo mensajes más nuevos que lastTimestamp.
    /// Si lastTimestamp es null, devuelve todos (carga inicial).
    /// Usa el índice IX_Messages_Conversation_SentAt.
    /// </summary>
    public async Task<IEnumerable<MessageDto>> GetDeltaMessagesAsync(int conversationId, DateTime? lastTimestamp)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, ConversationId, SenderId, Content, MessageType, IsRead, SentAt
            FROM Messages
            WHERE ConversationId = @ConversationId
              AND (@LastTimestamp IS NULL OR SentAt > @LastTimestamp)
              AND IsDeleted = 0
            ORDER BY SentAt ASC
            """;

        var rows = await conn.QueryAsync(sql, new
        {
            ConversationId = conversationId,
            LastTimestamp  = lastTimestamp
        });

        return rows.Select(MapRow);
    }

    /// <summary>
    /// Envía un mensaje y actualiza LastMessageAt en la conversación en la misma operación.
    /// SenderId viene del JWT (controlador), nunca del body.
    /// </summary>
    public async Task<MessageDto> SendAsync(int conversationId, int senderId, string content)
    {
        using var conn = await db.CreateAsync();

        // Verificar que la conversación existe y el sender es participante
        var check = await conn.QueryFirstOrDefaultAsync<(int BuyerId, int SellerId)>(
            "SELECT BuyerId, SellerId FROM Conversations WHERE Id = @Id",
            new { Id = conversationId });

        if (check == default)
            throw new KeyNotFoundException($"Conversación {conversationId} no encontrada.");

        if (check.BuyerId != senderId && check.SellerId != senderId)
            throw new UnauthorizedAccessException("No eres participante de esta conversación.");

        // INSERT y UPDATE LastMessageAt en una sola operación con OUTPUT
        const string sql = """
            INSERT INTO Messages (ConversationId, SenderId, Content, MessageType)
            OUTPUT INSERTED.Id, INSERTED.ConversationId, INSERTED.SenderId,
                   INSERTED.Content, INSERTED.MessageType, INSERTED.IsRead, INSERTED.SentAt
            VALUES (@ConversationId, @SenderId, @Content, 'Text');
            """;

        var row = await conn.QuerySingleAsync(sql, new
        {
            ConversationId = conversationId,
            SenderId       = senderId,
            Content        = content
        });

        // El trigger trg_Messages_UpdateConversationLastMessage ya actualiza LastMessageAt.
        // Si no existe el trigger en tu BD, descomenta esto:
        // await conn.ExecuteAsync(
        //     "UPDATE Conversations SET LastMessageAt = SYSDATETIME() WHERE Id = @Id",
        //     new { Id = conversationId });

        return MapRow(row);
    }

    /// <summary>Marca como leídos todos los mensajes del otro usuario en esta conversación.</summary>
    public async Task MarkAsReadAsync(int conversationId, int userId)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            UPDATE Messages
            SET IsRead = 1
            WHERE ConversationId = @ConversationId
              AND SenderId <> @UserId
              AND IsRead = 0
              AND IsDeleted = 0
            """;

        await conn.ExecuteAsync(sql, new
        {
            ConversationId = conversationId,
            UserId         = userId
        });
    }

    // ---------------------------------------------------------------
    // Helper de mapeo centralizado (evita duplicación)
    // ---------------------------------------------------------------
    private static MessageDto MapRow(dynamic r) => new(
        Id:             (int)r.Id,
        ConversationId: (int)r.ConversationId,
        SenderId:       (int)r.SenderId,
        Content:        (string)r.Content,
        MessageType:    (string)r.MessageType,
        IsRead:         r.IsRead == true,
        SentAt:         ((DateTime)r.SentAt).ToString("o"));
}
