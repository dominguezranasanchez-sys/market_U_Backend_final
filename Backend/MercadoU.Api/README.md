# MercadoU.Api

Backend REST para **MercadoU** — marketplace universitario.
Stack: **ASP.NET Core 8 · Dapper · SQL Server · JWT**

---

## Estructura del proyecto

```
MercadoU.Api/
├── Program.cs                          ← DI, CORS, JWT, pipeline
├── appsettings.json                    ← Config (NO subir a Git con credenciales reales)
├── MercadoU.Api.csproj
│
├── DTOs/
│   └── Dtos.cs                         ← C# 12 Records para todos los contratos
│
├── Application/
│   ├── Interfaces/
│   │   └── IRepositories.cs            ← Contratos de repositorios
│   └── Services/
│       └── AuthService.cs              ← BCrypt + JWT
│
├── Infrastructure/
│   ├── Data/
│   │   └── SqlConnectionFactory.cs     ← Factoría de conexiones Dapper
│   └── Repositories/
│       ├── ProductRepository.cs        ← SearchAsync usa sp_SearchProducts
│       ├── CatalogRepositories.cs      ← Category / Location / University
│       └── UserAndSocialRepositories.cs ← User / Favorite / Conversation / Message
│
└── Controllers/
    ├── ProductsController.cs
    └── AllControllers.cs               ← Auth / Users / Catalogs / Chat
```

---

## Configuración rápida

### 1. Cadena de conexión

**Opción A — appsettings.json** (desarrollo local, NO subir a Git):
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=tcp:...database.windows.net,1433;..."
}
```

**Opción B — Variable de entorno** (producción / CI):
```bash
export ConnectionStrings__DefaultConnection="Server=tcp:...;..."
```

### 2. JWT Secret

En `appsettings.json` o variable de entorno:
```json
"Jwt": {
  "Secret": "MINIMO_32_CARACTERES_ALEATORIOS_AQUI"
}
```
```bash
export Jwt__Secret="mi_super_secreto_largo..."
```

### 3. Ejecutar

```bash
cd MercadoU.Api
dotnet restore
dotnet run
# Swagger en https://localhost:7001/swagger
```

---

## Rutas implementadas (contrato con el frontend)

| Método | Ruta                                        | Frontend (api.ts)                         |
|--------|---------------------------------------------|-------------------------------------------|
| POST   | `/api/auth/login`                           | `UsersAPI.login()`                        |
| POST   | `/api/auth/register`                        | `UsersAPI.register()`                     |
| GET    | `/api/users/{id}`                           | `UsersAPI.get(id)`                        |
| GET    | `/api/users/{id}/products`                  | `ProductsAPI.bySeller(id)`                |
| GET    | `/api/users/{id}/reviews`                   | `UsersAPI.reviewsFor(id)`                 |
| GET    | `/api/users/{id}/favorites`                 | `FavoritesAPI.list(id)`                   |
| POST   | `/api/users/{userId}/favorites/{productId}` | `FavoritesAPI.toggle()`                   |
| GET    | `/api/users/{id}/conversations`             | `ChatAPI.conversations(id)`               |
| GET    | `/api/products`                             | `ProductsAPI.list(params)`                |
| GET    | `/api/products/{id}`                        | `ProductsAPI.get(id)`                     |
| POST   | `/api/products`                             | `ProductsAPI.create()`                    |
| GET    | `/api/products/{id}/images`                 | `ProductsAPI.loadImages(id)`              |
| POST   | `/api/products/{id}/images`                 | `ProductsAPI.addImage()`                  |
| GET    | `/api/categories`                           | `CategoriesAPI.list()`                    |
| GET    | `/api/locations`                            | `LocationsAPI.list()`                     |
| GET    | `/api/universities?locationId=`             | `UniversitiesAPI.list()`                  |
| POST   | `/api/conversations`                        | `ChatAPI.startWithSeller()`               |
| GET    | `/api/conversations/{id}/messages`          | `ChatAPI.messages(id)`                    |
| POST   | `/api/conversations/{id}/messages`          | `ChatAPI.send()`                          |

---

## Decisiones arquitectónicas importantes

### Triggers de BD — No replicar en C#
- `trg_Reviews_UpdateUserRating` → recalcula `Users.AverageRating` y `TotalReviews` automáticamente.
- `trg_Messages_UpdateConversationLastMessage` → actualiza `Conversations.LastMessageAt` al insertar mensajes.
- **El backend NO toca estos campos.** Solo los lee.

### Soft Delete
- Todas las queries incluyen `WHERE IsDeleted = 0` explícitamente.
- Nunca se hace `DELETE` físico desde el API.

### Keyset Pagination
- `GET /api/products` delega en `sp_SearchProducts` con parámetros `lastId` + `lastValue`.
- Compatible con scroll infinito en el frontend.

### CORS
- Política `AllowAnyOrigin` necesaria para los dominios de preview de Lovable (`*.lovable.app`).

### Variable de entorno en frontend (Lovable)
```
VITE_API_URL=https://tu-backend.azurewebsites.net/api
```
