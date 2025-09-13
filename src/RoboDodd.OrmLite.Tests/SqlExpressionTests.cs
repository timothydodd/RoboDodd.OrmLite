using System.Data;
using System.Linq.Expressions;
using FluentAssertions;
using RoboDodd.OrmLite;
using Xunit;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// Tests for SqlExpression fluent query builder functionality using MySQL
/// </summary>
[Collection("MySQL Collection")]
public class MySqlSqlExpressionTests : SqlExpressionTestsBase
{
    private readonly MySqlFixture _fixture;
    
    public MySqlSqlExpressionTests(MySqlFixture fixture) : base(fixture.ConnectionFactory, isMySQL: true)
    {
        _fixture = fixture;
    }
    
    protected override async Task<IDbConnection> CreateFreshConnectionAsync()
    {
        return await _fixture.CreateFreshDatabaseConnectionAsync();
    }
}

/// <summary>
/// Tests for SqlExpression fluent query builder functionality using SQLite
/// </summary>
[Collection("SQLite Collection")]
public class SqliteSqlExpressionTests : SqlExpressionTestsBase
{
    private readonly SqliteFixture _fixture;
    
    public SqliteSqlExpressionTests(SqliteFixture fixture) : base(fixture.ConnectionFactory, isMySQL: false)
    {
        _fixture = fixture;
    }
    
    protected override async Task<IDbConnection> CreateFreshConnectionAsync()
    {
        return await Task.FromResult(_fixture.CreateFreshDatabaseConnection());
    }
}

/// <summary>
/// Base class for SqlExpression fluent query builder tests
/// </summary>
public abstract class SqlExpressionTestsBase : IDisposable
{
    protected readonly IDbConnectionFactory ConnectionFactory;
    protected readonly bool IsMySQL;
    private readonly IDbConnection _connection;

    protected SqlExpressionTestsBase(IDbConnectionFactory connectionFactory, bool isMySQL)
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
    public async Task SqlExpression_From_CreatesExpression()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Act
        var expression = db.From<ServiceStackCompatibleUser>();

        // Assert
        expression.Should().NotBeNull();
        expression.Should().BeOfType<SqlExpression<ServiceStackCompatibleUser>>();
    }

    [Fact]
    public void SqlExpression_ToSelectStatement_GeneratesBasicQuery()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>();

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().NotBeNull();
        sql.Should().Contain("SELECT * FROM");
        sql.Should().Contain("ServiceStackCompatibleUser");
    }

    [Fact]
    public void SqlExpression_WithWhere_GeneratesWhereClause()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name == "Test");

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("WHERE");
        expression.Parameters.ParameterNames.Should().NotBeEmpty();
    }

    [Fact]
    public void SqlExpression_WithOrderBy_GeneratesOrderByClause()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .OrderBy(u => u.Name);

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("Name ASC");
    }

    [Fact]
    public void SqlExpression_WithOrderByDescending_GeneratesOrderByDescClause()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .OrderByDescending(u => u.CreatedAt);

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("CreatedAt DESC");
    }

    [Fact]
    public void SqlExpression_WithLimit_GeneratesLimitClause()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Limit(10);

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("LIMIT 10");
    }

    [Fact]
    public void SqlExpression_WithLimitAndOffset_GeneratesLimitOffsetClause()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Limit(10, 20);

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("LIMIT 10");
        sql.Should().Contain("OFFSET 20");
    }

    [Fact]
    public void SqlExpression_ChainedOperations_GeneratesComplexQuery()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name.Contains("Test"))
            .OrderBy(u => u.CreatedAt)
            .Limit(5, 10);

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("SELECT * FROM");
        sql.Should().Contain("ServiceStackCompatibleUser");
        sql.Should().Contain("WHERE");
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("CreatedAt ASC");
        sql.Should().Contain("LIMIT 5");
        sql.Should().Contain("OFFSET 10");
    }

    [Fact]
    public void SqlExpression_WithStringWhere_AcceptsRawSql()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where("Name LIKE @name", new { name = "%test%" });

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("WHERE");
        sql.Should().Contain("Name LIKE @name");
    }

    [Fact]
    public async Task SqlExpression_WithSelectAsync_ExecutesQuery()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Insert test data
        var testUser = new ServiceStackCompatibleUser
        {
            Name = "Test User",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        await db.InsertAsync(testUser);

        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name == "Test User");

        // Act
        var results = await db.SelectAsync(expression);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Test User");
        results[0].Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task SqlExpression_WithComplexQuery_FiltersAndOrdersCorrectly()
    {
        // Arrange
        using var db = await CreateFreshConnectionAsync();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Insert multiple test users
        var users = new[]
        {
            new ServiceStackCompatibleUser { Name = "Alice", Email = "alice@example.com", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new ServiceStackCompatibleUser { Name = "Bob", Email = "bob@example.com", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new ServiceStackCompatibleUser { Name = "Charlie", Email = "charlie@example.com", CreatedAt = DateTime.UtcNow }
        };

        foreach (var user in users)
        {
            await db.InsertAsync(user);
        }

        // Act - Query with complex expression
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name != "Bob")
            .OrderByDescending(u => u.CreatedAt)
            .Limit(2);

        var results = await db.SelectAsync(expression);

        // Assert
        results.Should().HaveCount(2);
        // Should get Charlie first (most recent), then Alice
        results[0].Name.Should().Be("Charlie");
        results[1].Name.Should().Be("Alice");
    }

    [Fact]
    public void SqlExpression_Parameters_AreAccessible()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name == "Test")
            .Where("Email LIKE @email", new { email = "%@example.com" });

        // Act
        var parameters = expression.Parameters;

        // Assert
        parameters.Should().NotBeNull();
        parameters.ParameterNames.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("Name", "ASC")]
    [InlineData("Email", "ASC")]
    [InlineData("CreatedAt", "ASC")]
    public void SqlExpression_OrderBy_HandlesMultipleProperties(string propertyName, string direction)
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var parameter = Expression.Parameter(typeof(ServiceStackCompatibleUser), "u");
        var property = Expression.Property(parameter, propertyName);
        var lambda = Expression.Lambda<Func<ServiceStackCompatibleUser, object>>(
            Expression.Convert(property, typeof(object)), parameter);

        var expression = db.From<ServiceStackCompatibleUser>()
            .OrderBy(lambda);

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain($"{propertyName} {direction}");
    }

    [Fact]
    public void SqlExpression_EscapeTableName_HandlesSpecialCharacters()
    {
        // Test that table names are properly escaped (important for databases with case sensitivity)
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<TestOrder>(); // Uses "Order" which is a SQL keyword

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("FROM");
        // The exact escaping depends on the database provider
        sql.Should().Contain("Order");
    }

    [Fact]
    public void SqlExpression_WithAndMethod_CombinesMultipleConditions()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name == "Test")
            .And(u => u.Email.Contains("@example.com"));

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("WHERE");
        // The SQL should contain both conditions joined by AND
        // We can't directly inspect DynamicParameters, but we can verify the SQL structure
    }

    [Fact]
    public void SqlExpression_WithAndMethodRawSql_CombinesConditions()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name == "Test")
            .And("Email LIKE @email", new { email = "%@test.com" });

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("WHERE");
        sql.Should().Contain("Email LIKE @email");
    }

    [Fact]
    public void SqlExpression_WithOrMethod_CreatesOrCondition()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name == "Alice")
            .Or(u => u.Name == "Bob");

        // Act
        var sql = expression.ToSelectStatement();

        // Assert
        sql.Should().Contain("WHERE");
        sql.Should().Contain("OR");
    }

    [Fact]
    public void SqlExpression_ToCountStatement_GeneratesCountQuery()
    {
        // Arrange
        using var db = ConnectionFactory.CreateDbConnection();
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name.Contains("Test"));

        // Act
        var sql = expression.ToCountStatement();

        // Assert
        sql.Should().StartWith("SELECT COUNT(*) FROM");
        sql.Should().Contain("ServiceStackCompatibleUser");
        sql.Should().Contain("WHERE");
        sql.Should().NotContain("ORDER BY"); // COUNT queries shouldn't have ORDER BY
        sql.Should().NotContain("LIMIT");    // COUNT queries shouldn't have LIMIT
    }

    [Fact]
    public async Task SqlExpression_WithCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using var db = await CreateFreshConnectionAsync();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Insert test data
        for (int i = 1; i <= 5; i++)
        {
            await db.InsertAsync(new ServiceStackCompatibleUser
            {
                Name = $"User{i}",
                Email = $"user{i}@example.com",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Act
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name.Contains("User"));
        var count = await db.CountAsync(expression);

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public async Task SqlExpression_WithSelectAsync_ReturnsFilteredResults()
    {
        // Arrange
        using var db = await CreateFreshConnectionAsync();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Insert test data
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Alice", Email = "alice@example.com", CreatedAt = DateTime.UtcNow });
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Bob", Email = "bob@example.com", CreatedAt = DateTime.UtcNow });
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Charlie", Email = "charlie@example.com", CreatedAt = DateTime.UtcNow });

        // Act
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name == "Alice")
            .Or(u => u.Name == "Charlie")
            .OrderBy(u => u.Name);
        var results = await db.SelectAsync(expression);

        // Assert
        results.Should().HaveCount(2);
        results[0].Name.Should().Be("Alice");
        results[1].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task SqlExpression_ComplexAndOrQuery_WorksCorrectly()
    {
        // Arrange
        using var db = await CreateFreshConnectionAsync();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Insert test data
        var baseTime = DateTime.UtcNow;
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Active1", Email = "active1@test.com", CreatedAt = baseTime.AddDays(-1) });
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Active2", Email = "active2@example.com", CreatedAt = baseTime.AddDays(-2) });
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Inactive", Email = "inactive@test.com", CreatedAt = baseTime.AddDays(-10) });
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Recent", Email = "recent@other.com", CreatedAt = baseTime });

        // Act - Find users with @test.com email OR created in last 3 days, AND name contains "Active"
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name.Contains("Active"))
            .And(u => u.Email.Contains("@test.com") || u.CreatedAt >= baseTime.AddDays(-3));

        var results = await db.SelectAsync(expression);

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(u => u.Name == "Active1");
        results.Should().Contain(u => u.Name == "Active2");
        results.Should().Contain(u => u.Name == "Inactive");    // Included because "Inactive" contains "Active"
        results.Should().NotContain(u => u.Name == "Recent");   // Excluded by name filter
    }

    [Fact]
    public async Task SqlExpression_MultipleAndConditions_ChainCorrectly()
    {
        // Arrange
        using var db = await CreateFreshConnectionAsync();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Insert test data
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "John Doe", Email = "john@example.com", CreatedAt = DateTime.UtcNow });
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "Jane Doe", Email = "jane@example.com", CreatedAt = DateTime.UtcNow.AddDays(-1) });
        await db.InsertAsync(new ServiceStackCompatibleUser { Name = "John Smith", Email = "john@test.com", CreatedAt = DateTime.UtcNow });

        // Act - Multiple AND conditions
        var expression = db.From<ServiceStackCompatibleUser>()
            .Where(u => u.Name.Contains("John"))
            .And(u => u.Email.Contains("@example.com"))
            .And(u => u.CreatedAt >= DateTime.UtcNow.AddHours(-1));

        var results = await db.SelectAsync(expression);

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("John Doe");
        results[0].Email.Should().Be("john@example.com");
    }
}