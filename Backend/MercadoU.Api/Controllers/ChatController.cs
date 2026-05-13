using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Dapper;
using MercadoU.Api.Services; 
using MercadoU.Api.DTOs;
using MercadoU.Api.Models;

[Authorize]
[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chat;
    private readonly string _connectionString;

    public ChatController(ChatService chat, IConfiguration config)
    {
        _chat = chat;
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }

    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("start")]
    public async Task<IActionResult> Start(StartChatDto dto)
    {
        var convo = await _chat.StartConversation(UserId, dto);
        return Ok(convo.Id);
    }

    [HttpPost("{id}/send")]
    public async Task<IActionResult> Send(Guid id, SendMessageDto dto)
    {
        var msg = await _chat.SendMessage(UserId, id, dto.Content);
        return Ok(msg);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMessages(Guid id)
    {
        using var db = new SqlConnection(_connectionString);
        var sql = "SELECT * FROM Messages WHERE ConversationId = @Id ORDER BY CreatedAt ASC";
        var messages = await db.QueryAsync<Message>(sql, new { Id = id });//The type or namespace name 'Message' could not be found (are you missing a using directive or an assembly reference?)
        return Ok(messages);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> Inbox()
    {
        using var db = new SqlConnection(_connectionString);
        // Consulta altamente optimizada que reemplaza el N+1 de EF
        var sql = @"
            SELECT 
                c.Id, 
                c.ProductId, 
                c.LastMessageAt,
                (SELECT COUNT(*) FROM Messages m WHERE m.ConversationId = c.Id AND m.IsRead = 0 AND m.SenderId != @UserId) as Unread
            FROM Conversations c
            WHERE c.BuyerId = @UserId OR c.SellerId = @UserId
            ORDER BY c.LastMessageAt DESC";
            
        var list = await db.QueryAsync(sql, new { UserId = UserId });
        return Ok(list);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        using var db = new SqlConnection(_connectionString);
        var sql = @"
            UPDATE Messages 
            SET IsRead = 1, ReadAt = @Now 
            WHERE ConversationId = @Id AND IsRead = 0 AND SenderId != @UserId";
            
        await db.ExecuteAsync(sql, new { Now = DateTime.UtcNow, Id = id, UserId = UserId });
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        using var db = new SqlConnection(_connectionString);
        var sql = "DELETE FROM Conversations WHERE Id = @Id";
        await db.ExecuteAsync(sql, new { Id = id });
        return Ok();
    }
}