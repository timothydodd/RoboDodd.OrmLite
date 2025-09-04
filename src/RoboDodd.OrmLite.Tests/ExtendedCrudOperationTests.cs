using System.Data;
using FluentAssertions;
using RoboDodd.OrmLite;
using Dapper;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// Tests for extended CRUD operations using MySQL Testcontainer
/// </summary>
[Collection("MySQL Collection")]
public class MySqlExtendedCrudOperationTests : ExtendedCrudOperationTestsBase
{
    public MySqlExtendedCrudOperationTests(MySqlFixture fixture) : base(fixture.ConnectionFactory)
    {
    }
}

/// <summary>
/// Tests for extended CRUD operations using SQLite
/// </summary>
[Collection("SQLite Collection")]
public class SqliteExtendedCrudOperationTests : ExtendedCrudOperationTestsBase
{
    public SqliteExtendedCrudOperationTests(SqliteFixture fixture) : base(fixture.ConnectionFactory)
    {
    }
}

/// <summary>
/// Base class containing all extended CRUD operation tests
/// </summary>
public abstract class ExtendedCrudOperationTestsBase : IDisposable
{
    protected readonly IDbConnectionFactory ConnectionFactory;
    private readonly IDbConnection _connection;

    protected ExtendedCrudOperationTestsBase(IDbConnectionFactory connectionFactory)
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

    #region Count Tests

    [Fact]
    public async Task CountAsync_ShouldReturnTotalCount_WhenNoFilter()
    {
        // Arrange - Insert test data
        var users = new[]
        {
            new TestUser { Name = "User1", Email = "user1@example.com", Age = 25, Balance = 1000m },
            new TestUser { Name = "User2", Email = "user2@example.com", Age = 35, Balance = 2000m },
            new TestUser { Name = "User3", Email = "user3@example.com", Age = 45, Balance = 3000m }
        };

        await _connection.InsertAllAsync(users);

        // Act
        var count = await _connection.CountAsync<TestUser>();

        // Assert
        count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnFilteredCount_WhenPredicateProvided()
    {
        // Arrange - Insert test data
        var users = new[]
        {
            new TestUser { Name = "Young1", Email = "young1@example.com", Age = 20, Balance = 1000m },
            new TestUser { Name = "Young2", Email = "young2@example.com", Age = 22, Balance = 1100m },
            new TestUser { Name = "Old1", Email = "old1@example.com", Age = 50, Balance = 5000m }
        };

        await _connection.InsertAllAsync(users);

        // Act
        var youngCount = await _connection.CountAsync<TestUser>(u => u.Age < 30);
        var oldCount = await _connection.CountAsync<TestUser>(u => u.Age >= 50);

        // Assert
        youngCount.Should().BeGreaterThanOrEqualTo(2);
        oldCount.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Exists Tests

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenEntityExists()
    {
        // Arrange
        var user = new TestUser { Name = "Exists Test", Email = "exists@example.com", Age = 30, Balance = 1000m };
        var id = await _connection.InsertAsync(user, selectIdentity: true);

        // Act
        var existsById = await _connection.ExistsAsync<TestUser>(id);
        var existsByPredicate = await _connection.ExistsAsync<TestUser>(u => u.Email == "exists@example.com");

        // Assert
        existsById.Should().BeTrue();
        existsByPredicate.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenEntityDoesNotExist()
    {
        // Act
        var existsById = await _connection.ExistsAsync<TestUser>(99999);
        var existsByPredicate = await _connection.ExistsAsync<TestUser>(u => u.Email == "nonexistent@example.com");

        // Assert
        existsById.Should().BeFalse();
        existsByPredicate.Should().BeFalse();
    }

    #endregion

    #region First/FirstOrDefault Tests

    [Fact]
    public async Task FirstAsync_ShouldReturnFirstMatch_WhenEntityExists()
    {
        // Arrange
        var users = new[]
        {
            new TestUser { Name = "First1", Email = "first1@example.com", Age = 25, Balance = 1000m },
            new TestUser { Name = "First2", Email = "first2@example.com", Age = 25, Balance = 1100m }
        };

        await _connection.InsertAllAsync(users);

        // Act
        var first = await _connection.FirstAsync<TestUser>(u => u.Age == 25);

        // Assert
        first.Should().NotBeNull();
        first!.Age.Should().Be(25);
    }

    [Fact]
    public async Task FirstAsync_ShouldThrowException_WhenNoEntityExists()
    {
        // Act & Assert
        var act = async () => await _connection.FirstAsync<TestUser>(u => u.Age == 999);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnFirstMatch_WhenEntityExists()
    {
        // Arrange
        var user = new TestUser { Name = "FirstOrDefault", Email = "firstordefault@example.com", Age = 30, Balance = 1000m };
        await _connection.InsertAsync(user);

        // Act
        var result = await _connection.FirstOrDefaultAsync<TestUser>(u => u.Age == 30);

        // Assert
        result.Should().NotBeNull();
        result!.Age.Should().Be(30);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnNull_WhenNoEntityExists()
    {
        // Act
        var result = await _connection.FirstOrDefaultAsync<TestUser>(u => u.Age == 999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Bulk Operations Tests

    [Fact]
    public async Task InsertAllAsync_ShouldInsertMultipleRecords_WhenValidEntities()
    {
        // Arrange
        var users = new[]
        {
            new TestUser { Name = "Bulk1", Email = "bulk1@example.com", Age = 25, Balance = 1000m },
            new TestUser { Name = "Bulk2", Email = "bulk2@example.com", Age = 35, Balance = 2000m },
            new TestUser { Name = "Bulk3", Email = "bulk3@example.com", Age = 45, Balance = 3000m }
        };

        // Act
        var affectedRows = await _connection.InsertAllAsync(users);

        // Assert
        affectedRows.Should().Be(3);

        // Verify the records exist
        var count = await _connection.CountAsync<TestUser>(u => u.Name.StartsWith("Bulk"));
        count.Should().Be(3);
    }

    [Fact]
    public async Task UpdateAllAsync_ShouldUpdateMultipleRecords_WhenValidEntities()
    {
        // Arrange - Insert test data
        var users = new List<TestUser>();
        for (int i = 1; i <= 3; i++)
        {
            var user = new TestUser { Name = $"BulkUpdate{i}", Email = $"bulkupdate{i}@example.com", Age = 30, Balance = 1000m };
            var id = await _connection.InsertAsync(user, selectIdentity: true);
            user.Id = (int)id;
            users.Add(user);
        }

        // Modify the entities
        foreach (var user in users)
        {
            user.Age = 35;
            user.Balance = 2000m;
        }

        // Act
        var affectedRows = await _connection.UpdateAllAsync(users);

        // Assert
        affectedRows.Should().Be(3);

        // Verify the updates
        var updatedUsers = await _connection.SelectAsync<TestUser>(u => u.Name.StartsWith("BulkUpdate"));
        updatedUsers.Should().AllSatisfy(u => u.Age.Should().Be(35));
        updatedUsers.Should().AllSatisfy(u => u.Balance.Should().Be(2000m));
    }

    [Fact]
    public async Task DeleteAllAsync_ShouldDeleteMultipleRecords_WhenValidEntities()
    {
        // Arrange - Insert test data
        var users = new List<TestUser>();
        for (int i = 1; i <= 3; i++)
        {
            var user = new TestUser { Name = $"BulkDelete{i}", Email = $"bulkdelete{i}@example.com", Age = 30, Balance = 1000m };
            var id = await _connection.InsertAsync(user, selectIdentity: true);
            user.Id = (int)id;
            users.Add(user);
        }

        // Act
        var affectedRows = await _connection.DeleteAllAsync(users);

        // Assert
        affectedRows.Should().Be(3);

        // Verify the deletions
        var count = await _connection.CountAsync<TestUser>(u => u.Name.StartsWith("BulkDelete"));
        count.Should().Be(0);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task SelectAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange - Insert test data
        var users = new List<TestUser>();
        for (int i = 1; i <= 10; i++)
        {
            users.Add(new TestUser { Name = $"Page{i:D2}", Email = $"page{i}@example.com", Age = 20 + i, Balance = 1000m * i });
        }
        await _connection.InsertAllAsync(users);

        // Act - Get second page with 3 items per page
        var page2 = await _connection.SelectAsync<TestUser>(3, 3); // Skip 3, Take 3

        // Assert
        page2.Should().HaveCount(3);
        // Note: We can't guarantee exact order without ORDER BY, but we can verify pagination works
        page2.Should().AllSatisfy(u => u.Name.Should().StartWith("Page"));
    }

    [Fact]
    public async Task SelectAsync_WithPredicateAndPagination_ShouldReturnFilteredPage()
    {
        // Arrange - Insert test data
        var users = new List<TestUser>();
        for (int i = 1; i <= 10; i++)
        {
            var age = i % 2 == 0 ? 30 : 20; // Even numbers get age 30, odd get age 20
            users.Add(new TestUser { Name = $"Filter{i:D2}", Email = $"filter{i}@example.com", Age = age, Balance = 1000m * i });
        }
        await _connection.InsertAllAsync(users);

        // Act - Get users with age 30, skip first 2, take 2
        var filteredPage = await _connection.SelectAsync<TestUser>(u => u.Age == 30, 2, 2);

        // Assert
        filteredPage.Should().HaveCountLessThanOrEqualTo(2);
        filteredPage.Should().AllSatisfy(u => u.Age.Should().Be(30));
        filteredPage.Should().AllSatisfy(u => u.Name.Should().StartWith("Filter"));
    }

    #endregion

    #region Save (Upsert) Tests

    [Fact]
    public async Task SaveAsync_ShouldInsert_WhenEntityIsNew()
    {
        // Arrange
        var user = new TestUser { Name = "Save Test", Email = "save@example.com", Age = 30, Balance = 1000m };

        // Act
        var result = await _connection.SaveAsync(user);

        // Assert
        result.Should().Be(1);
        user.Id.Should().BeGreaterThan(0); // Should have been set by the insert

        // Verify the entity exists
        var exists = await _connection.ExistsAsync<TestUser>(user.Id);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdate_WhenEntityExists()
    {
        // Arrange - Insert a user first
        var user = new TestUser { Name = "Save Update", Email = "saveupdate@example.com", Age = 30, Balance = 1000m };
        var id = await _connection.InsertAsync(user, selectIdentity: true);
        user.Id = (int)id;

        // Modify the user
        user.Age = 35;
        user.Balance = 2000m;

        // Act
        var result = await _connection.SaveAsync(user);

        // Assert
        result.Should().Be(1);

        // Verify the update
        var updated = await _connection.SingleByIdAsync<TestUser>(id);
        updated.Should().NotBeNull();
        updated!.Age.Should().Be(35);
        updated.Balance.Should().Be(2000m);
    }

    [Fact]
    public async Task SaveAsync_WithGuidKeys_ShouldWorkCorrectly()
    {
        // Arrange - New entity with empty GUID (should insert)
        var category = new TestCategory
        {
            Id = Guid.Empty, // This should trigger insert
            Name = "Save Category",
            Description = "Test category for save",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var insertResult = await _connection.SaveAsync(category);

        // Assert
        insertResult.Should().Be(1);

        // Now update it
        category.Id = Guid.NewGuid(); // Set a real GUID
        category.Description = "Updated description";
        
        // Insert it first so we have something to update
        await _connection.InsertAsync(category);
        
        // Now save should update
        category.Description = "Updated again";
        var updateResult = await _connection.SaveAsync(category);
        
        updateResult.Should().Be(1);
    }

    #endregion
}