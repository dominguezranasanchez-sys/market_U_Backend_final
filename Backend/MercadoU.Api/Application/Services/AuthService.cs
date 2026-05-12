using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.DTOs;
using Microsoft.IdentityModel.Tokens;

namespace MercadoU.Api.Application.Services;

public sealed class AuthService(
    IUserRepository userRepo,
    IConfiguration configuration)
{
    private readonly string _secret =
        configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret not configured.");

    private readonly string _issuer   = configuration["Jwt:Issuer"]   ?? "MercadoU.Api";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "MercadoU.Frontend";

    private readonly int _expiresHours =
        int.TryParse(configuration["Jwt:ExpiresInHours"], out var h) ? h : 72;

    // ------------------------------------------------------------------
    // LOGIN
    // ------------------------------------------------------------------
    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var result = await userRepo.GetByEmailAsync(req.Email);

        if (result is null || !BCrypt.Net.BCrypt.Verify(req.Password, result.Value.PasswordHash))
            throw new UnauthorizedAccessException("Credenciales inválidas.");

        var token = GenerateJwt(result.Value.User);
        return new AuthResponse(result.Value.User, token);
    }

    // ------------------------------------------------------------------
    // REGISTER
    // ------------------------------------------------------------------
    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12);
        var userId = await userRepo.CreateAsync(req, passwordHash);

        var user = await userRepo.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Error al recuperar el usuario recién creado.");

        var token = GenerateJwt(user);
        return new AuthResponse(user, token);
    }

    // ------------------------------------------------------------------
    // JWT factory
    // ------------------------------------------------------------------
    private string GenerateJwt(UserDto user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(_expiresHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("firstName", user.FirstName),
            new Claim("lastName",  user.LastName),
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
