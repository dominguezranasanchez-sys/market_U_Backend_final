namespace MercadoU.Api.DTOs 
{
    public class StartChatDto
    {
        public Guid ProductId { get; set; }
        public Guid SellerId { get; set; }
    }

    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
    }
}