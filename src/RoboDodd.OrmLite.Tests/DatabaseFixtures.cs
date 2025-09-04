using System.Data;
using RoboDodd.OrmLite;
using Testcontainers.MySql;
using Microsoft.Data.Sqlite;
using Dapper;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// MySQL test fixture using Testcontainers
/// </summary>
public class MySqlFixture : IAsyncLifetime
{
    private MySqlContainer? _container;
    
    public IDbConnectionFactory ConnectionFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new MySqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", "rootpass")
            .Build();

        await _container.StartAsync();
        
        var connectionString = _container.GetConnectionString();
        ConnectionFactory = new DbConnectionFactory(connectionString);
        
        // Create test tables
        await CreateTestTablesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
    
    private async Task CreateTestTablesAsync()
    {
        using var connection = await ConnectionFactory.OpenAsync();
        
        // Create test tables using our ORM extensions
        await connection.CreateTableIfNotExistsAsync<TestUser>();
        await connection.CreateTableIfNotExistsAsync<TestPost>();
        await connection.CreateTableIfNotExistsAsync<TestCategory>();
        await connection.CreateTableIfNotExistsAsync<TestOrder>();
    }
}

/// <summary>
/// SQLite test fixture using shared in-memory database
/// </summary>
public class SqliteFixture : IDisposable
{
    private readonly SharedSqliteConnectionFactory _connectionFactory;
    
    public IDbConnectionFactory ConnectionFactory => _connectionFactory;

    public SqliteFixture()
    {
        // Use a temporary file database that gets cleaned up
        var tempFile = Path.GetTempFileName();
        var connectionString = $"Data Source={tempFile};";
        _connectionFactory = new SharedSqliteConnectionFactory(connectionString);
        
        // Create test tables
        CreateTestTables();
    }

    public void Dispose()
    {
        _connectionFactory?.Dispose();
    }
    
    private void CreateTestTables()
    {
        // Create test tables using our ORM extensions
        var connection = _connectionFactory.CreateDbConnection();
        connection.CreateTableIfNotExistsAsync<TestUser>().Wait();
        connection.CreateTableIfNotExistsAsync<TestPost>().Wait();
        connection.CreateTableIfNotExistsAsync<TestCategory>().Wait();
        connection.CreateTableIfNotExistsAsync<TestOrder>().Wait();
    }
}

/// <summary>
/// Connection factory that returns the same connection for SQLite tests
/// </summary>
public class SharedSqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    private readonly IDbConnection _sharedConnection;

    public SharedSqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
        _sharedConnection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        _sharedConnection.Open();
        
        // Register type handlers for SQLite
        RegisterTypeHandlers();
    }
    
    private static bool _typeHandlersRegistered = false;
    
    private static void RegisterTypeHandlers()
    {
        if (!_typeHandlersRegistered)
        {
            // Register GUID type handler for SQLite
            Dapper.SqlMapper.AddTypeHandler(new RoboDodd.OrmLite.GuidTypeHandler());
            Dapper.SqlMapper.AddTypeHandler(new RoboDodd.OrmLite.NullableGuidTypeHandler());
            _typeHandlersRegistered = true;
        }
    }

    public IDbConnection CreateDbConnection()
    {
        return _sharedConnection;
    }

    public void Dispose()
    {
        _sharedConnection?.Dispose();
    }
}

/// <summary>
/// Collection fixture for MySQL tests
/// </summary>
[CollectionDefinition("MySQL Collection")]
public class MySqlCollectionFixture : ICollectionFixture<MySqlFixture>
{
    // This class has no code, and is never created. Its purpose is just
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

/// <summary>
/// Collection fixture for SQLite tests
/// </summary>
[CollectionDefinition("SQLite Collection")]
public class SqliteCollectionFixture : ICollectionFixture<SqliteFixture>
{
    // This class has no code, and is never created. Its purpose is just
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}