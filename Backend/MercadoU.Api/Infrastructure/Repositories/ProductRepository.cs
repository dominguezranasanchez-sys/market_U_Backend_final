using Dapper;
using MercadoU.Api.Application.Interfaces;
using MercadoU.Api.DTOs;
using MercadoU.Api.Infrastructure.Data;

namespace MercadoU.Api.Infrastructure.Repositories;

/// <summary>
/// Repositorio de Productos usando Dapper.
/// Todas las queries incluyen WHERE IsDeleted = 0 (soft-delete).
/// La conversión de Condition ('New', 'LikeNew', …) a ('new'/'used') se hace
/// en el mapper para mantener compatibilidad con el frontend.
/// </summary>
public sealed class ProductRepository(SqlConnectionFactory db) : IProductRepository
{
    // ------------------------------------------------------------------
    // Helper: normaliza Condition de BD → string que espera el frontend
    // ------------------------------------------------------------------
    private static string NormalizeCondition(string dbCondition) =>
        dbCondition is "New" ? "new" : "used";

    private static string NormalizeStatus(string dbStatus) =>
        dbStatus.ToLowerInvariant();   // 'Active' → 'active', 'Sold' → 'sold', etc.

    // ------------------------------------------------------------------
    // SEARCH  — delega al SP sp_SearchProducts (Keyset pagination)
    // ------------------------------------------------------------------
    public async Task<IEnumerable<ProductDto>> SearchAsync(ProductSearchParams p)
    {
        using var connection = await db.CreateAsync();

        // El SP acepta 'New'/'LikeNew'/… pero el FE manda 'new'/'used'.
        // Mapeamos: 'new' → 'New', 'used' → null (el SP filtra por valor exacto,
        // así que con null devuelve todos los usados).
        string? dbCondition = p.Condition?.ToLowerInvariant() switch
        {
            "new"  => "New",
            "used" => null,   // el SP devuelve todos si es NULL; no hay forma de filtrar sólo "usados" con el SP actual
            _      => null
        };

        var rows = await connection.QueryAsync(
            "sp_SearchProducts",
            new
            {
                CategoryId  = p.CategoryId,
                LocationId  = p.LocationId,
                MinPrice    = p.MinPrice,
                MaxPrice    = p.MaxPrice,
                Condition   = dbCondition,
                SearchText  = p.Q,
                SortBy      = p.SortBy,
                PageSize    = p.PageSize,
                LastId      = p.LastId,
                LastValue   = p.LastValue
            },
            commandType: System.Data.CommandType.StoredProcedure);

        return rows.Select(r => new ProductDto(
            Id:              (int)r.Id,
            Title:           (string)r.Title,
            Description:     string.Empty,   // SP no devuelve Description (optimización); se carga en GetById
            Price:           (decimal)r.Price,
            NegotiablePrice: r.NegotiablePrice,
            SellerId:        (int)r.SellerId,
            CategoryId:      (int?)r.CategoryId ?? 0,
            LocationId:      (int?)r.LocationId,
            UniversityId:    null,
            Condition:       NormalizeCondition((string)(r.Condition ?? "Used")),
            Status:          "active",
            ViewCount:       (int)r.ViewCount,
            FavoriteCount:   (int)r.FavoriteCount,
            PrimaryImageUrl: (string?)r.PrimaryImageUrl,
            CreatedAt:       r.PublishedAt is not null
                             ? ((DateTime)r.PublishedAt).ToString("o")
                             : DateTime.UtcNow.ToString("o")));
    }

    // ------------------------------------------------------------------
    // GET BY ID
    // ------------------------------------------------------------------
    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        using var connection = await db.CreateAsync();

        const string sql = """
            SELECT
                p.Id, p.Title, p.Description, p.Price, p.NegotiablePrice,
                p.SellerId, p.CategoryId, p.LocationId, p.Condition, p.Status,
                p.ViewCount, p.FavoriteCount, p.CreatedAt,
                pi_img.Url AS PrimaryImageUrl
            FROM Products p
            LEFT JOIN ProductImages pi_img
                ON pi_img.ProductId = p.Id AND pi_img.IsPrimary = 1
            WHERE p.Id = @Id
              AND p.IsDeleted = 0
            """;

        var row = await connection.QueryFirstOrDefaultAsync(sql, new { Id = id });
        if (row is null) return null;

        return new ProductDto(
            Id:              (int)row.Id,
            Title:           (string)row.Title,
            Description:     (string)row.Description,
            Price:           (decimal)row.Price,
            NegotiablePrice: row.NegotiablePrice == true,
            SellerId:        (int)row.SellerId,
            CategoryId:      (int)row.CategoryId,
            LocationId:      (int?)row.LocationId,
            UniversityId:    null,
            Condition:       NormalizeCondition((string)row.Condition),
            Status:          NormalizeStatus((string)row.Status),
            ViewCount:       (int)row.ViewCount,
            FavoriteCount:   (int)row.FavoriteCount,
            PrimaryImageUrl: (string?)row.PrimaryImageUrl,
            CreatedAt:       ((DateTime)row.CreatedAt).ToString("o"));
    }

    // ------------------------------------------------------------------
    // GET BY SELLER
    // ------------------------------------------------------------------
    public async Task<IEnumerable<ProductDto>> GetBySellerAsync(int sellerId)
    {
       using var conn = await db.CreateAsync();

        const string sql = """
            SELECT
                p.Id, p.Title, p.Description, p.Price, p.NegotiablePrice,
                p.SellerId, p.CategoryId, p.LocationId, p.Condition, p.Status,
                p.ViewCount, p.FavoriteCount, p.CreatedAt,
                pi_img.Url AS PrimaryImageUrl
            FROM Products p
            LEFT JOIN ProductImages pi_img
                ON pi_img.ProductId = p.Id AND pi_img.IsPrimary = 1
            WHERE p.SellerId = @SellerId
              AND p.IsDeleted = 0
            ORDER BY p.CreatedAt DESC
            """;

        var rows = await conn.QueryAsync(sql, new { SellerId = sellerId });

        return rows.Select(r => new ProductDto(
            Id:              (int)r.Id,
            Title:           (string)r.Title,
            Description:     (string)r.Description,
            Price:           (decimal)r.Price,
            NegotiablePrice: r.NegotiablePrice == true,
            SellerId:        (int)r.SellerId,
            CategoryId:      (int)r.CategoryId,
            LocationId:      (int?)r.LocationId,
            UniversityId:    null,
            Condition:       NormalizeCondition((string)r.Condition),
            Status:          NormalizeStatus((string)r.Status),
            ViewCount:       (int)r.ViewCount,
            FavoriteCount:   (int)r.FavoriteCount,
            PrimaryImageUrl: (string?)r.PrimaryImageUrl,
            CreatedAt:       ((DateTime)r.CreatedAt).ToString("o")));
    }

    // ------------------------------------------------------------------
    // CREATE
    // ------------------------------------------------------------------
    public async Task<int> CreateAsync(CreateProductRequest req)
    {
        using var conn = await db.CreateAsync();

        // El frontend manda condition: 'new'|'used'.  La BD acepta: New|LikeNew|Good|Fair|Poor.
        string dbCondition = req.Condition.ToLowerInvariant() == "new" ? "New" : "Good";

        const string sql = """
            INSERT INTO Products
                (Title, Description, Price, NegotiablePrice, Condition,
                 SellerId, CategoryId, LocationId, Status, PublishedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@Title, @Description, @Price, @NegotiablePrice, @Condition,
                 @SellerId, @CategoryId, @LocationId, 'Active', SYSDATETIME())
            """;

        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            req.Title,
            req.Description,
            req.Price,
            NegotiablePrice = req.NegotiablePrice ? 1 : 0,
            Condition = dbCondition,
            req.SellerId,
            req.CategoryId,
            req.LocationId
        });
    }

    // ------------------------------------------------------------------
    // IMAGES
    // ------------------------------------------------------------------
    public async Task<IEnumerable<ProductImageDto>> GetImagesAsync(int productId)
    {
        using var conn = await db.CreateAsync();

        const string sql = """
            SELECT ProductId, Url, IsPrimary, DisplayOrder
            FROM ProductImages
            WHERE ProductId = @ProductId
            ORDER BY IsPrimary DESC, DisplayOrder ASC
            """;

        return await conn.QueryAsync<ProductImageDto>(sql, new { ProductId = productId });
    }

    public async Task<ProductImageDto> AddImageAsync(int productId, AddImageRequest req)
    {
       using var conn = await db.CreateAsync();

        // Si es primaria, quitar la bandera de las existentes
        if (req.IsPrimary)
        {
            await conn.ExecuteAsync(
                "UPDATE ProductImages SET IsPrimary = 0 WHERE ProductId = @ProductId",
                new { ProductId = productId });
        }

        const string sql = """
            INSERT INTO ProductImages (ProductId, Url, IsPrimary, DisplayOrder)
            OUTPUT INSERTED.ProductId, INSERTED.Url, INSERTED.IsPrimary, INSERTED.DisplayOrder
            VALUES (@ProductId, @Url, @IsPrimary,
                    ISNULL((SELECT MAX(DisplayOrder)+1 FROM ProductImages WHERE ProductId = @ProductId), 0))
            """;

        return await conn.QuerySingleAsync<ProductImageDto>(sql, new
        {
            ProductId = productId,
            req.Url,
            IsPrimary = req.IsPrimary ? 1 : 0
        });
    }
}
