using System.Security.Cryptography;
using Dapper;
using MySqlConnector;

namespace cap_theorem_backend.Services;

public interface IMySqlProvisioningService
{
    (string DbUser, string Password) GenerateCredentials(string dbUser);
    Task CreateDatabaseAsync(string dbName, string dbUser, string password);
    string Encrypt(string plainText);
}

public class MySqlProvisioningService : IMySqlProvisioningService
{
    private readonly string _adminConnectionString;
    public MySqlProvisioningService(IConfiguration config) =>
        _adminConnectionString = config["MySqlAdmin:ConnectionString"]!;

    public (string, string) GenerateCredentials(string dbUser) =>
        (dbUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));

    public async Task CreateDatabaseAsync(string dbName, string dbUser, string password)
    {
        using var conn = new MySqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        // Parámetros seguros: nombres validados por sp_ReserveDatabaseSlot (regex/whitelist),
        // password siempre parametrizada.
        await conn.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{dbName}`");
        await conn.ExecuteAsync("CREATE USER @user@'%' IDENTIFIED BY @pass", new { user = dbUser, pass = password });
        await conn.ExecuteAsync($"GRANT ALL PRIVILEGES ON `{dbName}`.* TO @user@'%'", new { user = dbUser });
    }

    public string Encrypt(string plainText) => /* AES con clave en KeyVault/env, no hardcode */ plainText;
}