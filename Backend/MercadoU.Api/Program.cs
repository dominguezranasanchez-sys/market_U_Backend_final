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
// 1. CORS - Configurado para Cloudflare y Localhost
// ==========================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "https://mercadou-frontend-v2.dominguez-rana-sanchez.workers.dev", // Tu link de Cloudflare
                "http://localhost:5173",
                "http://localhost:8080"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Necesario para Auth
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
// 3. Rate Limiting
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
// 4. Inyección de Dependencias (SQL Factory y Repos)
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

// FORZAR PUERTO 10000 PARA RENDER
builder.WebHost.ConfigureKestrel(options => {
    options.ListenAnyIP(10000);
});

var app = builder.Build();

// ==========================================================================
// PIPELINE (ORDEN IMPORTANTE)
// ==========================================================================
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MercadoU API V1");
    c.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// COMENTADO PARA EVITAR ERROR 502 EN RENDER
// app.UseHttpsRedirection(); 

app.UseCors("FrontendCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllers().RequireRateLimiting("BasicRateLimit");

app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

app.Run(); // Línea 166: Ahora debería iniciar sin Error 139