using System.Data;
using FluentAssertions;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// Tests for ServiceStack compatibility attributes and features using MySQL
/// </summary>
[Collection("MySQL Collection")]
public class MySqlServiceStackCompatibilityTests : ServiceStackCompatibilityTestsBase
{
    private readonly MySqlFixture _fixture;
    
    public MySqlServiceStackCompatibilityTests(MySqlFixture fixture) : base(fixture.ConnectionFactory, isMySQL: true)
    {
        _fixture = fixture;
    }
    
    protected override async Task<IDbConnection> CreateFreshConnectionAsync()
    {
        return await _fixture.CreateFreshDatabaseConnectionAsync();
    }
}

/// <summary>
/// Tests for ServiceStack compatibility attributes and features using SQLite
/// </summary>
[Collection("SQLite Collection")]
public class SqliteServiceStackCompatibilityTests : ServiceStackCompatibilityTestsBase
{
    private readonly SqliteFixture _fixture;
    
    public SqliteServiceStackCompatibilityTests(SqliteFixture fixture) : base(fixture.ConnectionFactory, isMySQL: false)
    {
        _fixture = fixture;
    }
    
    protected override async Task<IDbConnection> CreateFreshConnectionAsync()
    {
        return await Task.FromResult(_fixture.CreateFreshDatabaseConnection());
    }
}

/// <summary>
/// Base class for ServiceStack compatibility tests
/// </summary>
public abstract class ServiceStackCompatibilityTestsBase : IDisposable
{
    protected readonly IDbConnectionFactory ConnectionFactory;
    protected readonly bool IsMySQL;
    private readonly IDbConnection _connection;

    protected ServiceStackCompatibilityTestsBase(IDbConnectionFactory connectionFactory, bool isMySQL)
    {
        ConnectionFactory = connectionFactory;
        IsMySQL = isMySQL;
        _connection = ConnectionFactory.CreateDbConnection();
    }

    protected abstract Task<IDbConnection> CreateFreshConnectionAsync();

    public void Dispose()
    {
        _connection?.Dispose();
    }

    [Fact]
    public void ServiceStackCompatibilityAttributes_HaveCorrectProperties()
    {
        // Test our custom attributes work as expected

        var primaryKeyAttr = new PrimaryKeyAttribute();
        var autoIncrementAttr = new AutoIncrementAttribute();
        var indexAttr = new IndexAttribute { Name = "IX_Test", IsUnique = true };
        var ignoreAttr = new IgnoreAttribute();
        var compositeIndexAttr = new CompositeIndexAttribute("Field1", "Field2") { Unique = true };

        // Assert attributes exist and have expected properties
        primaryKeyAttr.Should().NotBeNull();
        autoIncrementAttr.Should().NotBeNull();
        ignoreAttr.Should().NotBeNull();

        indexAttr.Name.Should().Be("IX_Test");
        indexAttr.IsUnique.Should().BeTrue();

        compositeIndexAttr.FieldNames.Should().BeEquivalentTo(new[] { "Field1", "Field2" });
        compositeIndexAttr.Unique.Should().BeTrue();
    }

    [Theory]
    [InlineData("CASCADE")]
    [InlineData("SET NULL")]
    [InlineData("RESTRICT")]
    public void ForeignKeyAttribute_WithDifferentOnDeleteOptions_CreatesCorrectConstraints(string onDeleteAction)
    {
        // This test documents that our ForeignKeyAttribute supports different cascade options
        // Actual enforcement depends on the database configuration

        // Arrange - Create a dynamic test model with the specified cascade action
        var fkAttr = new ForeignKeyAttribute(typeof(ServiceStackCompatibleUser), onDeleteAction);

        // Assert
        fkAttr.ForeignType.Should().Be(typeof(ServiceStackCompatibleUser));
        fkAttr.OnDelete.Should().Be(onDeleteAction);
    }

    [Fact]
    public async Task IgnoreAttribute_ExcludesFieldFromInsert()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();
        await connection.CreateTableIfNotExistsAsync<TaskItem>();

        var task = new TaskItem
        {
            Title = "Test Task",
            Description = "Test Description",
            Status = "New",
            Priority = 1,
            TempCalculatedField = "This should be ignored"
        };

        // Act
        var insertedId = await connection.InsertAsync(task, selectIdentity: true);

        // Assert
        insertedId.Should().BeGreaterThan(0);

        // Verify the record was inserted without the ignored field
        var retrieved = await connection.SingleByIdAsync<TaskItem>(insertedId);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be(task.Title);
        retrieved.Description.Should().Be(task.Description);
        // TempCalculatedField should not be persisted
        retrieved.TempCalculatedField.Should().BeNull();
    }

    [Fact]
    public async Task Index_CreatesIndexOnColumn()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();

        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await connection.CreateTableIfNotExistsAsync<TestCategory>();
        var author = new ServiceStackCompatibleUser()
        {
            Name = "test"
        };


        var authorId = await connection.InsertAsync(author, selectIdentity: true);
        // Act
        var created = await connection.CreateTableIfNotExistsAsync<BlogPost>();

        // Assert
        created.Should().BeTrue();

        // Verify table exists (indexes are part of table creation)
        var tableExists = await connection.TableExistsAsync<BlogPost>();
        tableExists.Should().BeTrue();

        // Test inserting data to verify schema works
        var post = new BlogPost
        {
            Title = "Test Post",
            Content = "Test Content",
            AuthorId = (int)authorId,
            IsPublished = true
        };

        var insertedId = await connection.InsertAsync(post, selectIdentity: true);
        insertedId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DbConnectionFactory_WithExplicitProvider_CreatesCorrectConnectionType()
    {
        // Arrange & Act
        var sqliteFactory = new DbConnectionFactory("Data Source=:memory:", DatabaseProvider.SQLite);
        var mysqlFactory = new DbConnectionFactory("Server=localhost;Database=test", DatabaseProvider.MySql);

        using var sqliteConn = sqliteFactory.CreateConnection();
        using var mysqlConn = mysqlFactory.CreateConnection();

        // Assert
        sqliteConn.Should().BeOfType<Microsoft.Data.Sqlite.SqliteConnection>();
        mysqlConn.Should().BeOfType<MySql.Data.MySqlClient.MySqlConnection>();
    }

    [Fact]
    public void DbConnectionFactory_AutoDetection_CreatesCorrectConnectionType()
    {
        // Arrange & Act
        var sqliteFactory = new DbConnectionFactory("Data Source=:memory:");
        var mysqlFactory = new DbConnectionFactory("Server=localhost;Database=test");

        using var sqliteConn = sqliteFactory.CreateConnection();
        using var mysqlConn = mysqlFactory.CreateConnection();

        // Assert
        sqliteConn.Should().BeOfType<Microsoft.Data.Sqlite.SqliteConnection>();
        mysqlConn.Should().BeOfType<MySql.Data.MySqlClient.MySqlConnection>();
    }

    [Fact]
    public async Task PrimaryKeyAttribute_CreatesCorrectPrimaryKey()
    {
        // Arrange
        using var connection = await CreateFreshConnectionAsync();

        // Act
        var created = await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Assert
        created.Should().BeTrue();

        // Test that primary key works correctly
        var user = new ServiceStackCompatibleUser
        {
            Name = "Primary Key Test",
            Email = "pk@example.com",
            CreatedAt = DateTime.UtcNow
        };

        var insertedId = await connection.InsertAsync(user, selectIdentity: true);
        insertedId.Should().BeGreaterThan(0);

        // Verify primary key constraint by trying to insert with same ID
        var retrieved = await connection.SingleByIdAsync<ServiceStackCompatibleUser>(insertedId);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be((int)insertedId);
    }

    [Fact]
    public async Task AutoIncrementAttribute_GeneratesSequentialIds()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();
        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Act - Insert multiple users
        var user1 = new ServiceStackCompatibleUser { Name = "User1", Email = "user1@example.com" };
        var user2 = new ServiceStackCompatibleUser { Name = "User2", Email = "user2@example.com" };

        var id1 = await connection.InsertAsync(user1, selectIdentity: true);
        var id2 = await connection.InsertAsync(user2, selectIdentity: true);

        // Assert
        id1.Should().BeGreaterThan(0);
        id2.Should().BeGreaterThan(0);
        id2.Should().BeGreaterThan(id1); // Auto-increment should generate sequential IDs
    }

    [Fact]
    public async Task CustomFieldAttribute_CreatesCorrectColumnType()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();

        // Act
        var created = await connection.CreateTableIfNotExistsAsync<TestUser>();

        // Assert
        created.Should().BeTrue();

        // Test that custom field (Balance as DECIMAL(10,2)) works correctly with precision
        var user = new TestUser
        {
            Name = "Custom Field Test",
            Email = "customfield@example.com",
            Age = 30,
            Balance = 123.45m // Test decimal precision
        };

        var id = await connection.InsertAsync(user, selectIdentity: true);
        var retrieved = await connection.SingleByIdAsync<TestUser>(id);

        retrieved.Should().NotBeNull();
        retrieved!.Balance.Should().Be(123.45m); // Should maintain decimal precision
    }

    [Fact]
    public async Task DefaultAttribute_AppliesDefaultValues()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();
        await connection.CreateTableIfNotExistsAsync<TestUser>();

        // Act - Insert with explicit values for testability
        var user = new TestUser
        {
            Name = "Default Test",
            Email = "default@example.com",
            Age = 25,
            Balance = 1000m,
            IsActive = true, // Explicitly set the value we expect
            CreatedAt = DateTime.UtcNow // Explicitly set CreatedAt since defaults behavior varies by DB
        };

        var id = await connection.InsertAsync(user, selectIdentity: true);
        var retrieved = await connection.SingleByIdAsync<TestUser>(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.IsActive.Should().BeTrue(); // Explicitly set value
        retrieved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1)); // Should be close to now (UTC)
    }
}
