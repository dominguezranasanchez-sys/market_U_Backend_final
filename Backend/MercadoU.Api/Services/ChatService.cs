using System.Data.SqlClient;
using Dapper;
using MercadoU.Api.DTOs;
using MercadoU.Api.Models;

namespace MercadoU.Api.Services
{
    public class ChatService
    {
        private readonly string _connectionString;

        public ChatService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<Conversation> StartConversation(Guid buyerId, StartChatDto dto)
        {
            using var db = new SqlConnection(_connectionString);
            
            var checkSql = @"
                SELECT * FROM Conversations 
                WHERE ProductId = @ProductId AND BuyerId = @BuyerId AND SellerId = @SellerId";
            
            var existing = await db.QueryFirstOrDefaultAsync<Conversation>(checkSql, 
                new { dto.ProductId, BuyerId = buyerId, dto.SellerId });

            if (existing != null)
                return existing;

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                BuyerId = buyerId,
                SellerId = dto.SellerId,
                ProductId = dto.ProductId,
                CreatedAt = DateTime.UtcNow
            };

            var insertSql = @"
                INSERT INTO Conversations (Id, BuyerId, SellerId, ProductId, CreatedAt) 
                VALUES (@Id, @BuyerId, @SellerId, @ProductId, @CreatedAt)";
            
            await db.ExecuteAsync(insertSql, conversation);
            return conversation;
        }

        public async Task<Message> SendMessage(Guid userId, Guid conversationId, string content)
        {
            using var db = new SqlConnection(_connectionString);
            
            var convoSql = "SELECT * FROM Conversations WHERE Id = @Id";
            var convo = await db.QueryFirstOrDefaultAsync<Conversation>(convoSql, new { Id = conversationId });

            if (convo == null) throw new Exception("Conversation not found");
            if (userId != convo.BuyerId && userId != convo.SellerId) throw new UnauthorizedAccessException();

            var message = new Message
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = userId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            var insertMsgSql = @"
                INSERT INTO Messages (Id, ConversationId, SenderId, Content, CreatedAt, IsRead) 
                VALUES (@Id, @ConversationId, @SenderId, @Content, @CreatedAt, @IsRead);
                
                UPDATE Conversations 
                SET LastMessageAt = @CreatedAt 
                WHERE Id = @ConversationId;";

            await db.ExecuteAsync(insertMsgSql, message);
            return message;
        }
    }
}