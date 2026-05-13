// ARCHIVO ELIMINADO — no borrar el archivo físicamente para no romper el .csproj
// La lógica que contenía ha sido consolidada en AllControllers.cs → ConversationsController
// que usa int IDs y Dapper correctamente.
//
// Razones de eliminación:
// 1. Usaba Guid en lugar de int para IDs (incompatible con el esquema SQL real)
// 2. Usaba SqlConnection directo en lugar de SqlConnectionFactory (rompía el patrón)
// 3. Dependía de ChatService que usaba Guid y modelo obsoleto
// 4. Referenciaba Message (Models/ChatModels.cs) que no existe en el namespace de DTOs
// 5. Ruta /api/chat conflictúa con el frontend que llama a /api/conversations
