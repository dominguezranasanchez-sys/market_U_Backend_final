using Dapper;
using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.DTOs;
using MercadoU.Api.Infrastructure.Data;

namespace MercadoU.Api.Infrastructure.Repositories;

public sealed class CategoryRepository(SqlConnectionFactory db) : ICategoryRepository
{
    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
       using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, Name, Slug, ParentCategoryId, DisplayOrder
            FROM Categories
            WHERE IsActive = 1
            ORDER BY DisplayOrder ASC, Name ASC
            """;

        return await conn.QueryAsync<CategoryDto>(sql);
    }
}

public sealed class LocationRepository(SqlConnectionFactory db) : ILocationRepository
{
    public async Task<IEnumerable<LocationDto>> GetAllAsync()
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, Country, State, City, ZipCode
            FROM Locations
            ORDER BY Country, State, City
            """;

        return await conn.QueryAsync<LocationDto>(sql);
    }
}

public sealed class UniversityRepository(SqlConnectionFactory db) : IUniversityRepository
{
    public async Task<IEnumerable<UniversityDto>> GetAllAsync(int? locationId)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT Id, Name, Acronym, LocationId
            FROM Universities
            WHERE (@LocationId IS NULL OR LocationId = @LocationId)
            ORDER BY Name ASC
            """;

        return await conn.QueryAsync<UniversityDto>(sql, new { LocationId = locationId });
    }
}
