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
                "https://tu-frontend-mercadou.azurewebsites.net",
                "http://localhost:8080",
                "http://192.168.56.1:8080",
                "http://192.168.1.154:8080",
                "http://localhost:5173"
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
    ?? throw new InvalidOperationException("Jwt:Secret not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)
            ),
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
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));
});

// ==========================================================================
// 4. Connection Factory
// ==========================================================================
builder.Services.AddScoped<SqlConnectionFactory>();

// ==========================================================================
// 5. Repositories
// ==========================================================================
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IUniversityRepository, UniversityRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();

// ==========================================================================
// 6. Services
// ==========================================================================
builder.Services.AddScoped<AuthService>();

// ==========================================================================
// 7. Controllers
// ==========================================================================
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;

        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ==========================================================================
// 8. Swagger
// ==========================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==========================================================================
// BUILD
// ==========================================================================
var app = builder.Build();

// ==========================================================================
// PIPELINE
// ==========================================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("FrontendCorsPolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers()
   .RequireRateLimiting("BasicRateLimit");

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow
    }));

app.Run();