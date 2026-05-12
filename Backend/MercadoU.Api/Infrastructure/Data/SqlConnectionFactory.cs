using Microsoft.Data.SqlClient;
using System.Data;

namespace MercadoU.Api.Infrastructure.Data;

/// <summary>
/// Factoría de conexiones Dapper.
/// Inyectada como Scoped → una conexión por request HTTP.
/// NUNCA expone la cadena de conexión fuera de esta clase.
/// </summary>
public sealed class SqlConnectionFactory(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' not found in configuration. " +
            "Set it in appsettings.json or via the CONNECTIONSTRINGS__DEFAULTCONNECTION environment variable.");

    /// <summary>
    /// Crea y abre una nueva conexión SQL Server lista para usar con Dapper.
    /// El llamador es responsable de cerrarla (using / await using).
    /// </summary>
    public async Task<IDbConnection> CreateAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
