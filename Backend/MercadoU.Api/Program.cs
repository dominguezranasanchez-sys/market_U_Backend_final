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
// 1. CORS
// ==========================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "https://peppy-crumble-705bb6.netlify.app",
                "http://localhost:5173",
                "http://localhost:8080"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ==========================================================================
// 2. JWT Authentication
// ==========================================================================
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret no configurado. Añádelo en las variables de entorno de Render.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer   = true,
            ValidIssuer      = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience    = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew        = TimeSpan.Zero
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
                Window      = TimeSpan.FromMinutes(1)
            }));
});

// ==========================================================================
// 4. Inyección de Dependencias — Dapper, sin ChatService ni ChatController
// ==========================================================================
builder.Services.AddScoped<SqlConnectionFactory>();
builder.Services.AddScoped<IProductRepository,      ProductRepository>();
builder.Services.AddScoped<ICategoryRepository,     CategoryRepository>();
builder.Services.AddScoped<ILocationRepository,     LocationRepository>();
builder.Services.AddScoped<IUniversityRepository,   UniversityRepository>();
builder.Services.AddScoped<IUserRepository,         UserRepository>();
builder.Services.AddScoped<IFavoriteRepository,     FavoriteRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository,      MessageRepository>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Puerto Render
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(10000);
});

var app = builder.Build();

// ==========================================================================
// PIPELINE — el orden es crítico
// ==========================================================================
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MercadoU API V1");
    c.RoutePrefix = "swagger";
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// CORS PRIMERO — antes de auth
app.UseCors("FrontendCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers().RequireRateLimiting("BasicRateLimit");

// Health check para Render
app.MapGet("/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

app.Run();
