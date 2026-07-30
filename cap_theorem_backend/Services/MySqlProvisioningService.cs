using System.Security.Cryptography;
using Dapper;
using MySqlConnector;

namespace cap_theorem_backend.Services;

public interface IMySqlProvisioningService
{
    (string DbUser, string Password) GenerateCredentials(string dbUser);
    Task CreateDatabaseAsync(string dbName, string dbUser, string password);
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public class MySqlProvisioningService : IMySqlProvisioningService
{
    private readonly string _adminConnectionString;
    private readonly byte[] _encryptionKey;

    public MySqlProvisioningService(IConfiguration config)
    {
        _adminConnectionString = config["MySqlAdmin:ConnectionString"]!;
        var keyBase64 = config["Encryption:Key"] ?? "M0x0RXlwM250U0VjcmV0S2V5Rm9yREJIdWIhMjAyNiM=";
        _encryptionKey = Convert.FromBase64String(keyBase64);
    }

    public (string, string) GenerateCredentials(string dbUser) =>
        (dbUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));

    public async Task CreateDatabaseAsync(string dbName, string dbUser, string password)
    {
        using var conn = new MySqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{dbName.Replace("`", "``")}`");
        var sql = $"CREATE USER `{dbUser.Replace("`", "``")}`@'%' IDENTIFIED BY @pass";
        await conn.ExecuteAsync(sql, new { pass = password });
        var grantSql = $"GRANT ALL PRIVILEGES ON `{dbName.Replace("`", "``")}`.* TO `{dbUser.Replace("`", "``")}`@'%'";
        await conn.ExecuteAsync(grantSql);
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var fullBytes = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        var iv = new byte[aes.BlockSize / 8];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = new byte[fullBytes.Length - iv.Length];
        Buffer.BlockCopy(fullBytes, iv.Length, cipherBytes, 0, cipherBytes.Length);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
