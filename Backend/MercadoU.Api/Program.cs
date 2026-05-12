using System.Text;
using System.Threading.RateLimiting;
using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.Application.Services;
using MercadoU.Api.Infrastructure.Data;
using MercadoU.Api.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ==========================================================================
// 1. CORS - Configurado para permitir a tu Frontend en Cloudflare
// ==========================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "https://mercadou-frontend-v2.dominguez-rana-sanchez.workers.dev", 
                "http://localhost:5173",
                "http://localhost:8080",
                "update-worker-name-to-mercadou-frontend-v2-mercadou-frontend-v2.dominguez-rana-sanchez.workers.dev",
                "https://peppy-crumble-705bb6.netlify.app" 
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Necesario si manejas cookies o headers de Auth
    });
});

// ==========================================================================
// 2. JWT Authentication
// ==========================================================================
var jwtSecret = builder.Configuration["Jwt:Secret"] 
    ?? "TuClaveSuperSecretaQueDeberiasPonerEnRenderVariables"; 

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ==========================================================================
// 3. Rate Limiting (Protección contra spam)
// ==========================================================================
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("BasicRateLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// ==========================================================================
// 4. Inyección de Dependencias
// ==========================================================================
builder.Services.AddScoped<SqlConnectionFactory>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IUniversityRepository, UniversityRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddControllers()
    .AddJsonOptions(opts => {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// FORZAR PUERTO 10000 PARA RENDER (Soluciona el error de puerto)
builder.WebHost.ConfigureKestrel(options => {
    options.ListenAnyIP(10000);
});

var app = builder.Build();

// ==========================================================================
// PIPELINE (EL ORDEN ES CRÍTICO AQUÍ)
// ==========================================================================

// Swagger debe estar al principio para pruebas
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MercadoU API V1");
    c.RoutePrefix = "swagger"; // Acceso en /swagger
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// IMPORTANTE: UseCors debe ir ANTES de Authentication y MapControllers
app.UseCors("FrontendCorsPolicy");  // ✅ este queda
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
// app.UseCors("Web");  ← ELIMINA esta línea
app.MapControllers().RequireRateLimiting("BasicRateLimit");

// Endpoint de salud para Render
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

app.Run();