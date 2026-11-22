using Microsoft.Data.SqlClient;
using Xunit;

namespace ManageU.Tests;

public class SqlServerConnectionTest
{
    [Fact]
    public async Task CanConnectToSqlServerExpress()
    {
        var connectionString = "Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.True(connection.State == System.Data.ConnectionState.Open);
    }
}
