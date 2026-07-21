using System.Security.Cryptography;
using Dapper;
using MySqlConnector;

namespace cap_theorem_backend.Services;

public interface IMySqlProvisioningService
{
    /// Genera la contraseña en texto plano que se envía a sp_ProvisionDatabase
    /// (el cifrado para almacenamiento ocurre dentro del SP, nunca aquí).
    string GenerateSecurePassword();

    Task CreateDatabaseAsync(string dbName, string dbUser, string password);
}

public class MySqlProvisioningService : IMySqlProvisioningService
{
    private readonly string _adminConnectionString;

    public MySqlProvisioningService(IConfiguration config) =>
        _adminConnectionString = config["MySqlAdmin:ConnectionString"]
            ?? throw new InvalidOperationException("Missing MySqlAdmin:ConnectionString");

    public string GenerateSecurePassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    public async Task CreateDatabaseAsync(string dbName, string dbUser, string password)
    {
        using var conn = new MySqlConnection(_adminConnectionString);
        await conn.OpenAsync();

        // dbName/dbUser vienen de fn_GenerateDbName (SQL Server), no de input
        // libre del usuario, por lo que el nombre entre backticks es seguro.
        // La contraseña siempre va parametrizada.
        await conn.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{dbName}`");
        await conn.ExecuteAsync("CREATE USER @user@'%' IDENTIFIED BY @pass", new { user = dbUser, pass = password });
        await conn.ExecuteAsync($"GRANT ALL PRIVILEGES ON `{dbName}`.* TO @user@'%'", new { user = dbUser });
        await conn.ExecuteAsync("FLUSH PRIVILEGES");
    }
}
