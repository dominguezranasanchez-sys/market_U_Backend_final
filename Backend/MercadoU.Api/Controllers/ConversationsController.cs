// ARCHIVO HISTÓRICO - LA LÓGICA HA SIDO MIGRADA
//
// Este archivo es solo referencia histórica.
// Todos los endpoints están en: AllControllers.cs → ConversationsController (SEALED)
//
// ENDPOINTS ACTIVOS EN AllControllers.cs:
// ==========================================
// POST   /api/conversations                     Crear conversación
// GET    /api/conversations                     Obtener inbox
// GET    /api/conversations/{id}/messages       Historial de mensajes
// POST   /api/conversations/{id}/messages       Enviar mensaje (GUARDA DATOS)
// PUT    /api/conversations/{id}/read           Marcar como leído
// DELETE /api/conversations/{id}                Archivar conversación
//
// ¿QUÉ ESTABA MAL AQUÍ?
// =====================
// 1. Colisión de rutas → ambos declaraban [Route("api/conversations")]
// 2. Method SendMessage tenía await Task.CompletedTask (no guardaba nada)
// 3. Referenciaba DTOs obsoletos con Guid en lugar de int
// 4. Método GetConversationsByUserIdAsync no existía en la interfaz
//
// CAMBIOS REALIZADOS:
// ===================
// ✓ Eliminar este controlador duplicado
// ✓ Confiar en AllControllers.ConversationsController
// ✓ Todos los endpoints funcionan correctamente allá
//
// TIPOS CORRECTOS (en Dtos.cs):
// =============================
// StartConversationFromFrontendRequest { int SellerId, int ProductId }
// SendMessageBodyRequest { string Content }
// ConversationInboxDto { ... con datos enriquecidos }
// MessageDto { ... con SentAt, no CreatedAt }