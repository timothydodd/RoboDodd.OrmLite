using System.Data;
using RoboDodd.OrmLite;
using Testcontainers.MySql;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
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
        // Don't create tables automatically in fixture - let each test create what it needs
        // This prevents issues where tests expect CreateTableIfNotExistsAsync to return true
        // when testing table creation functionality
        await Task.CompletedTask;
    }

    /// <summary>
    /// Helper method for tests that need all common tables created
    /// </summary>
    public async Task EnsureCommonTablesExistAsync()
    {
        using var connection = await ConnectionFactory.OpenAsync();
        
        await connection.CreateTableIfNotExistsAsync<TestUser>();
        await connection.CreateTableIfNotExistsAsync<TestPost>();
        await connection.CreateTableIfNotExistsAsync<TestCategory>();
        await connection.CreateTableIfNotExistsAsync<TestOrder>();
        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await connection.CreateTableIfNotExistsAsync<UserProfile>();
        await connection.CreateTableIfNotExistsAsync<BlogPost>();
        await connection.CreateTableIfNotExistsAsync<TaskItem>();
    }

    /// <summary>
    /// Creates a fresh MySQL database connection for isolated testing
    /// Each call creates a new database with a unique name
    /// </summary>
    public async Task<IDbConnection> CreateFreshDatabaseConnectionAsync()
    {
        if (_container == null) throw new InvalidOperationException("MySQL container not initialized");
        
        var uniqueDbName = $"testdb_{Guid.NewGuid():N}";
        var baseConnectionString = _container.GetConnectionString();
        
        // Create the new database using root connection
        var rootConnectionString = baseConnectionString.Replace("testuser", "root").Replace("testpass", "rootpass");
        using (var adminConnection = new MySqlConnection(rootConnectionString))
        {
            await adminConnection.OpenAsync();
            await adminConnection.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{uniqueDbName}`");
            // Grant privileges to testuser for this database
            await adminConnection.ExecuteAsync($"GRANT ALL PRIVILEGES ON `{uniqueDbName}`.* TO 'testuser'@'%'");
            await adminConnection.ExecuteAsync("FLUSH PRIVILEGES");
        }
        
        // Return connection to the new database
        var newConnectionString = baseConnectionString.Replace("testdb", uniqueDbName);
        var connection = new MySqlConnection(newConnectionString);
        await connection.OpenAsync();
        return connection;
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
        // Don't create tables automatically in fixture - let each test create what it needs
        // This prevents issues where tests expect CreateTableIfNotExistsAsync to return true
        // when testing table creation functionality
    }

    /// <summary>
    /// Creates a fresh SQLite database connection for isolated testing
    /// Each call creates a new temporary database file
    /// </summary>
    public IDbConnection CreateFreshDatabaseConnection()
    {
        var tempFile = Path.GetTempFileName();
        var connectionString = $"Data Source={tempFile};";
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        // Register type handlers for this connection
        SharedSqliteConnectionFactory.RegisterTypeHandlers();
        
        return connection;
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
    
    public static void RegisterTypeHandlers()
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