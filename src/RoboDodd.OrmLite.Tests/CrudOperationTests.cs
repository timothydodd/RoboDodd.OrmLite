using System.Data;
using FluentAssertions;
using RoboDodd.OrmLite;
using Dapper;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// Tests for CRUD operations using MySQL Testcontainer
/// </summary>
[Collection("MySQL Collection")]
public class MySqlCrudOperationTests : CrudOperationTestsBase
{
    public MySqlCrudOperationTests(MySqlFixture fixture) : base(fixture.ConnectionFactory)
    {
    }
}

/// <summary>
/// Tests for CRUD operations using SQLite
/// </summary>
[Collection("SQLite Collection")]
public class SqliteCrudOperationTests : CrudOperationTestsBase
{
    public SqliteCrudOperationTests(SqliteFixture fixture) : base(fixture.ConnectionFactory)
    {
    }
}

/// <summary>
/// Base class containing all CRUD operation tests
/// </summary>
public abstract class CrudOperationTestsBase : IDisposable
{
    protected readonly IDbConnectionFactory ConnectionFactory;
    private readonly IDbConnection _connection;

    protected CrudOperationTestsBase(IDbConnectionFactory connectionFactory)
    {
        ConnectionFactory = connectionFactory;
        _connection = ConnectionFactory.CreateDbConnection();
        _connection.Open();
        
        // Clean up any existing test data
        CleanupTestData();
    }
    
    private void CleanupTestData()
    {
        try
        {
            _connection.ExecuteAsync("DELETE FROM test_users").Wait();
            _connection.ExecuteAsync("DELETE FROM test_posts").Wait();
            _connection.ExecuteAsync("DELETE FROM test_categories").Wait();
            _connection.ExecuteAsync("DELETE FROM `Order`").Wait();
        }
        catch
        {
            // Ignore errors if tables don't exist
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    [Fact]
    public async Task InsertAsync_ShouldInsertRecord_WhenValidEntity()
    {
        // Arrange
        var user = new TestUser
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Age = 30,
            Balance = 1500.50m,
            Notes = "Test user"
        };

        // Act
        var id = await _connection.InsertAsync(user, selectIdentity: true);

        // Assert
        id.Should().BeGreaterThan(0);
        user.Id = (int)id; // Set the ID for further tests
        
        // Verify the record exists
        var retrieved = await _connection.SingleByIdAsync<TestUser>(id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(user.Name);
        retrieved.Email.Should().Be(user.Email);
        retrieved.Age.Should().Be(user.Age);
        retrieved.Balance.Should().Be(user.Balance);
    }

    [Fact]
    public async Task SelectAsync_ShouldReturnAllRecords_WhenNoFilter()
    {
        // Arrange - Insert test data
        var users = new[]
        {
            new TestUser { Name = "Alice", Email = "alice@example.com", Age = 25, Balance = 1000m },
            new TestUser { Name = "Bob", Email = "bob@example.com", Age = 35, Balance = 2000m }
        };

        foreach (var user in users)
        {
            await _connection.InsertAsync(user);
        }

        // Act
        var results = await _connection.SelectAsync<TestUser>();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCountGreaterThanOrEqualTo(2);
        results.Should().Contain(u => u.Name == "Alice");
        results.Should().Contain(u => u.Name == "Bob");
    }

    [Fact]
    public async Task SelectAsync_WithPredicate_ShouldReturnFilteredRecords()
    {
        // Arrange - Insert test data
        var youngUser = new TestUser { Name = "Young User", Email = "young@example.com", Age = 20, Balance = 500m };
        var oldUser = new TestUser { Name = "Old User", Email = "old@example.com", Age = 50, Balance = 3000m };

        await _connection.InsertAsync(youngUser);
        await _connection.InsertAsync(oldUser);

        // Act
        var results = await _connection.SelectAsync<TestUser>(u => u.Age > 25);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(u => u.Name == "Old User");
        results.Should().NotContain(u => u.Name == "Young User");
    }

    [Fact]
    public async Task SingleByIdAsync_ShouldReturnRecord_WhenExists()
    {
        // Arrange
        var user = new TestUser { Name = "Single Test", Email = "single@example.com", Age = 25, Balance = 1000m };
        var id = await _connection.InsertAsync(user, selectIdentity: true);

        // Act
        var result = await _connection.SingleByIdAsync<TestUser>(id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be(user.Name);
        result.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task SingleByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _connection.SingleByIdAsync<TestUser>(99999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SingleAsync_WithPredicate_ShouldReturnSingleRecord()
    {
        // Arrange
        var user = new TestUser { Name = "Unique Name", Email = "unique@example.com", Age = 25, Balance = 1000m };
        await _connection.InsertAsync(user);

        // Act
        var result = await _connection.SingleAsync<TestUser>(u => u.Email == "unique@example.com");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Unique Name");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateRecord_WhenExists()
    {
        // Arrange
        var user = new TestUser { Name = "Original", Email = "original@example.com", Age = 25, Balance = 1000m };
        var id = await _connection.InsertAsync(user, selectIdentity: true);
        user.Id = (int)id;

        // Act
        user.Name = "Updated";
        user.Age = 30;
        var affectedRows = await _connection.UpdateAsync(user);

        // Assert
        affectedRows.Should().Be(1);

        // Verify the update
        var updated = await _connection.SingleByIdAsync<TestUser>(id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated");
        updated.Age.Should().Be(30);
        updated.Email.Should().Be("original@example.com"); // Should remain unchanged
    }

    [Fact]
    public async Task UpdateOnlyAsync_ShouldUpdateSpecificFields_WhenExists()
    {
        // Arrange
        var user = new TestUser { Name = "Original", Email = "original@example.com", Age = 25, Balance = 1000m };
        var id = await _connection.InsertAsync(user, selectIdentity: true);

        // Act
        var affectedRows = await _connection.UpdateOnlyAsync<TestUser>(
            () => new TestUser { Name = "Partially Updated", Age = 35 },
            u => u.Id == (int)id);

        // Assert
        affectedRows.Should().Be(1);

        // Verify the update
        var updated = await _connection.SingleByIdAsync<TestUser>(id);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Partially Updated");
        updated.Age.Should().Be(35);
        updated.Email.Should().Be("original@example.com"); // Should remain unchanged
        updated.Balance.Should().Be(1000m); // Should remain unchanged
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteRecord_WhenExists()
    {
        // Arrange
        var user = new TestUser { Name = "To Delete", Email = "delete@example.com", Age = 25, Balance = 1000m };
        var id = await _connection.InsertAsync(user, selectIdentity: true);
        user.Id = (int)id;

        // Act
        var affectedRows = await _connection.DeleteAsync(user);

        // Assert
        affectedRows.Should().Be(1);

        // Verify deletion
        var deleted = await _connection.SingleByIdAsync<TestUser>(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteByIdAsync_ShouldDeleteRecord_WhenExists()
    {
        // Arrange
        var user = new TestUser { Name = "To Delete By ID", Email = "deletebyid@example.com", Age = 25, Balance = 1000m };
        var id = await _connection.InsertAsync(user, selectIdentity: true);

        // Act
        var affectedRows = await _connection.DeleteByIdAsync<TestUser>(id);

        // Assert
        affectedRows.Should().Be(1);

        // Verify deletion
        var deleted = await _connection.SingleByIdAsync<TestUser>(id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithPredicate_ShouldDeleteMatchingRecords()
    {
        // Arrange
        var users = new[]
        {
            new TestUser { Name = "Delete Me 1", Email = "del1@example.com", Age = 20, Balance = 500m },
            new TestUser { Name = "Delete Me 2", Email = "del2@example.com", Age = 21, Balance = 600m },
            new TestUser { Name = "Keep Me", Email = "keep@example.com", Age = 30, Balance = 1000m }
        };

        foreach (var user in users)
        {
            await _connection.InsertAsync(user);
        }

        // Act
        var affectedRows = await _connection.DeleteAsync<TestUser>(u => u.Age < 25);

        // Assert
        affectedRows.Should().Be(2);

        // Verify that only the matching records were deleted
        var remaining = await _connection.SelectAsync<TestUser>(u => u.Email.Contains("@example.com"));
        remaining.Should().HaveCount(1);
        remaining.First().Name.Should().Be("Keep Me");
    }

    [Fact]
    public async Task CRUD_WithGuidKeys_ShouldWorkCorrectly()
    {
        // Arrange
        var category = new TestCategory
        {
            Id = Guid.NewGuid(),
            Name = "Technology",
            Description = "Tech-related posts",
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert - Insert
        var affectedRows = await _connection.InsertAsync(category);
        affectedRows.Should().BeGreaterThan(-1); // Some databases return 0 for non-identity inserts

        // Act & Assert - Select by ID
        var retrieved = await _connection.SingleByIdAsync<TestCategory>(category.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Technology");
        retrieved.Description.Should().Be("Tech-related posts");

        // Act & Assert - Update
        category.Description = "Updated description";
        var updateRows = await _connection.UpdateAsync(category);
        updateRows.Should().Be(1);

        // Verify update
        var updated = await _connection.SingleByIdAsync<TestCategory>(category.Id);
        updated!.Description.Should().Be("Updated description");

        // Act & Assert - Delete
        var deleteRows = await _connection.DeleteAsync(category);
        deleteRows.Should().Be(1);

        // Verify deletion
        var deleted = await _connection.SingleByIdAsync<TestCategory>(category.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task CRUD_WithSqlKeywordColumns_ShouldWorkCorrectly()
    {
        // Arrange
        var order = new TestOrder
        {
            OrderName = "Test Order",
            OrderDate = DateTime.UtcNow,
            OrderValue = 299.99m,
            UserName = "test.user"
        };

        // Act & Assert - Insert (SQL keywords should be properly escaped)
        var id = await _connection.InsertAsync(order, selectIdentity: true);
        id.Should().BeGreaterThan(0);

        // Act & Assert - Select by ID (SQL keywords should be properly escaped)
        var retrieved = await _connection.SingleByIdAsync<TestOrder>(id);
        retrieved.Should().NotBeNull();
        retrieved!.OrderName.Should().Be("Test Order");
        retrieved.UserName.Should().Be("test.user");

        // Act & Assert - Update (SQL keywords should be properly escaped)
        order.Id = (int)id;
        order.OrderValue = 399.99m;
        var updateRows = await _connection.UpdateAsync(order);
        updateRows.Should().Be(1);

        // Verify update
        var updated = await _connection.SingleByIdAsync<TestOrder>(id);
        updated!.OrderValue.Should().Be(399.99m);

        // Act & Assert - Delete (SQL keywords should be properly escaped)
        var deleteRows = await _connection.DeleteAsync(order);
        deleteRows.Should().Be(1);
    }
}