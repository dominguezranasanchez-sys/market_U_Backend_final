// ARCHIVO HISTÓRICO - DTOs MIGRADOS A Dtos.cs
//
// Este archivo contenía DTOs obsoletos:
//   - StartChatDto { Guid ProductId, Guid SellerId } ← INCORRECTO
//   - SendMessageDto { string Content }
//
// DTOs CORRECTOS están en Dtos.cs:
//   - StartConversationFromFrontendRequest { int SellerId, int ProductId }
//   - SendMessageBodyRequest { string Content }
//   - ConversationInboxDto { ... con datos enriquecidos }
//   - MessageDto { ... con SentAt en lugar de CreatedAt }
//
// Problema anterior:
// Frontend envía INT, pero DTO esperaba GUID → type mismatch
//
// Solución:
// Usar Dtos.cs que tiene tipos correctos.