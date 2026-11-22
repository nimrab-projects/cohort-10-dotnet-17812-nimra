using Microsoft.Data.SqlClient;
using Xunit;

namespace ManageU.Tests;

public class DatabaseSchemaVerificationTests
{
    [Fact]
    public async Task VerifyDatabaseTablesAndIndexesCreated()
    {
        var connectionString = "Server=localhost\\SQLEXPRESS;Database=ManageUDb;Trusted_Connection=True;TrustServerCertificate=True;";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // 1. Verify tables
        var tables = new List<string>();
        await using (var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Contains("Tasks", tables);
        Assert.Contains("Categories", tables);
        Assert.Contains("AspNetUsers", tables);
        Assert.Contains("AspNetRoles", tables);
        Assert.Contains("__EFMigrationsHistory", tables);

        // 2. Verify indexes on Tasks table
        var taskIndexes = new List<string>();
        await using (var cmd = new SqlCommand("SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('Tasks') AND name IS NOT NULL", connection))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                taskIndexes.Add(reader.GetString(0));
            }
        }

        Assert.Contains("IX_Tasks_AssignedToUserId_Status", taskIndexes);
        Assert.Contains("IX_Tasks_CreatedByUserId", taskIndexes);
        Assert.Contains("IX_Tasks_CategoryId", taskIndexes);
        Assert.Contains("IX_Tasks_DueDate_Status", taskIndexes);
    }
}
