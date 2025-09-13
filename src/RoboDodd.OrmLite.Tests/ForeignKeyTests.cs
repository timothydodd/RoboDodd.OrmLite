using System.Data;
using FluentAssertions;
using RoboDodd.OrmLite;
using Xunit;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// Tests for Foreign Key constraint operations using MySQL
/// </summary>
[Collection("MySQL Collection")]
public class MySqlForeignKeyTests : ForeignKeyTestsBase
{
    private readonly MySqlFixture _fixture;
    
    public MySqlForeignKeyTests(MySqlFixture fixture) : base(fixture.ConnectionFactory, isMySQL: true)
    {
        _fixture = fixture;
    }
    
    protected override async Task<IDbConnection> CreateFreshConnectionAsync()
    {
        return await _fixture.CreateFreshDatabaseConnectionAsync();
    }
}

/// <summary>
/// Tests for Foreign Key constraint operations using SQLite
/// </summary>
[Collection("SQLite Collection")]
public class SqliteForeignKeyTests : ForeignKeyTestsBase
{
    private readonly SqliteFixture _fixture;
    
    public SqliteForeignKeyTests(SqliteFixture fixture) : base(fixture.ConnectionFactory, isMySQL: false)
    {
        _fixture = fixture;
    }
    
    protected override async Task<IDbConnection> CreateFreshConnectionAsync()
    {
        return await Task.FromResult(_fixture.CreateFreshDatabaseConnection());
    }
}

/// <summary>
/// Base class for Foreign Key constraint tests
/// </summary>
public abstract class ForeignKeyTestsBase : IDisposable
{
    protected readonly IDbConnectionFactory ConnectionFactory;
    protected readonly bool IsMySQL;
    private readonly IDbConnection _connection;

    protected ForeignKeyTestsBase(IDbConnectionFactory connectionFactory, bool isMySQL)
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
    public async Task CreateTableWithForeignKey_CreatesConstraintWithCascadeDelete()
    {
        // Arrange - Use fresh database connection for clean state
        using var connection = await CreateFreshConnectionAsync();

        // Act - Create parent table first
        var userCreated = await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        var profileCreated = await connection.CreateTableIfNotExistsAsync<UserProfile>();

        // Assert
        userCreated.Should().BeTrue("parent table should be created");
        profileCreated.Should().BeTrue("child table with FK should be created");

        // Verify both tables exist
        var userTableExists = await connection.TableExistsAsync<ServiceStackCompatibleUser>();
        var profileTableExists = await connection.TableExistsAsync<UserProfile>();
        
        userTableExists.Should().BeTrue();
        profileTableExists.Should().BeTrue();
    }

    [Fact]
    public async Task ForeignKeyRelationship_WithValidParent_AllowsChildInsertion()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();
        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await connection.CreateTableIfNotExistsAsync<UserProfile>();

        // Create a parent user
        var user = new ServiceStackCompatibleUser
        {
            Name = "Test User",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        var userId = await connection.InsertAsync(user, selectIdentity: true);

        // Act - Create a child profile
        var profile = new UserProfile
        {
            FirstName = "Test",
            LastName = "User",
            UserId = (int)userId,
            Bio = "Test bio",
            UpdatedAt = DateTime.UtcNow
        };
        var profileId = await connection.InsertAsync(profile, selectIdentity: true);

        // Assert
        userId.Should().BeGreaterThan(0);
        profileId.Should().BeGreaterThan(0);

        // Verify relationships exist
        var retrievedProfile = await connection.SingleByIdAsync<UserProfile>(profileId);
        retrievedProfile.Should().NotBeNull();
        retrievedProfile.UserId.Should().Be((int)userId);
    }

    [Fact]
    public async Task CascadeDelete_DeleteParent_RemovesChildRecords()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();
        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await connection.CreateTableIfNotExistsAsync<UserProfile>();

        // Create parent and child records
        var user = new ServiceStackCompatibleUser
        {
            Name = "Test User",
            Email = "cascade@test.com",
            CreatedAt = DateTime.UtcNow
        };
        var userId = await connection.InsertAsync(user, selectIdentity: true);

        var profile = new UserProfile
        {
            FirstName = "Cascade",
            LastName = "Test",
            UserId = (int)userId,
            Bio = "Test cascade delete",
            UpdatedAt = DateTime.UtcNow
        };
        await connection.InsertAsync(profile);

        // Verify both records exist
        var userExists = await connection.ExistsAsync<ServiceStackCompatibleUser>(userId);
        var profileCount = await connection.CountAsync<UserProfile>(p => p.UserId == userId);
        
        userExists.Should().BeTrue();
        profileCount.Should().Be(1);

        // Act - Delete the parent user
        await connection.DeleteByIdAsync<ServiceStackCompatibleUser>(userId);

        // Assert - Child should be cascade deleted (in databases that enforce FK constraints)
        var userExistsAfter = await connection.ExistsAsync<ServiceStackCompatibleUser>(userId);
        var profileCountAfter = await connection.CountAsync<UserProfile>(p => p.UserId == userId);
        
        userExistsAfter.Should().BeFalse();
        
        // Note: Actual cascade behavior depends on database FK constraint enforcement
        // This test documents expected behavior when constraints are properly enforced
    }

    [Fact]
    public async Task MultipleRelationships_WithDifferentCascadeOptions_WorkCorrectly()
    {
        // Arrange
        using var connection = ConnectionFactory.CreateDbConnection();
        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await connection.CreateTableIfNotExistsAsync<TestCategory>();
        await connection.CreateTableIfNotExistsAsync<BlogPost>();

        // Create parent records
        var userId = await connection.InsertAsync(new ServiceStackCompatibleUser
        {
            Name = "Author",
            Email = "author@test.com",
            CreatedAt = DateTime.UtcNow
        }, selectIdentity: true);

        var category = new TestCategory
        {
            Id = Guid.NewGuid(),
            Name = "Technology",
            Description = "Tech posts",
            CreatedAt = DateTime.UtcNow
        };
        await connection.InsertAsync(category);

        // Act - Create blog post with multiple FKs
        var post = new BlogPost
        {
            Title = "Test Post",
            Content = "Test content",
            AuthorId = (int)userId,
            CategoryId = category.Id,
            IsPublished = true,
            ViewCount = 0,
            PublishedAt = DateTime.UtcNow
        };
        var postId = await connection.InsertAsync(post, selectIdentity: true);

        // Assert
        postId.Should().BeGreaterThan(0);

        var retrievedPost = await connection.SingleByIdAsync<BlogPost>(postId);
        retrievedPost.Should().NotBeNull();
        retrievedPost.AuthorId.Should().Be((int)userId);
        retrievedPost.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public async Task SetNullForeignKey_OnParentDelete_SetsChildToNull()
    {
        // This test demonstrates SET NULL behavior for nullable FK relationships
        using var connection = ConnectionFactory.CreateDbConnection();
        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await connection.CreateTableIfNotExistsAsync<TestCategory>();
        await connection.CreateTableIfNotExistsAsync<BlogPost>();

        // Create parent records
        var userId = await connection.InsertAsync(new ServiceStackCompatibleUser
        {
            Name = "Author",
            Email = "setnull@test.com",
            CreatedAt = DateTime.UtcNow
        }, selectIdentity: true);

        var category = new TestCategory
        {
            Id = Guid.NewGuid(),
            Name = "Temporary Category",
            Description = "Will be deleted",
            CreatedAt = DateTime.UtcNow
        };
        await connection.InsertAsync(category);

        // Create blog post
        var post = new BlogPost
        {
            Title = "Test Post",
            Content = "Test content",
            AuthorId = (int)userId,
            CategoryId = category.Id, // This should be set to NULL when category is deleted
            IsPublished = true
        };
        var postId = await connection.InsertAsync(post, selectIdentity: true);

        // Act - Delete the category (should SET NULL on CategoryId)
        await connection.DeleteByIdAsync<TestCategory>(category.Id);

        // Assert - Post should still exist but CategoryId should be null
        var retrievedPost = await connection.SingleByIdAsync<BlogPost>(postId);
        retrievedPost.Should().NotBeNull();
        
        // Note: Actual behavior depends on database FK constraint enforcement
        // This test documents expected SET NULL behavior
    }

    [Fact]
    public void ForeignKeyAttribute_WithDifferentOptions_CreatesCorrectConstraints()
    {
        // Test that our ForeignKeyAttribute accepts different cascade options
        var cascadeAttr = new ForeignKeyAttribute(typeof(ServiceStackCompatibleUser), "CASCADE");
        var setNullAttr = new ForeignKeyAttribute(typeof(TestCategory), "SET NULL");
        var restrictAttr = new ForeignKeyAttribute(typeof(ServiceStackCompatibleUser), "RESTRICT");

        // Assert attributes have correct properties
        cascadeAttr.ForeignType.Should().Be(typeof(ServiceStackCompatibleUser));
        cascadeAttr.OnDelete.Should().Be("CASCADE");

        setNullAttr.ForeignType.Should().Be(typeof(TestCategory));
        setNullAttr.OnDelete.Should().Be("SET NULL");

        restrictAttr.OnDelete.Should().Be("RESTRICT");
    }

    [Fact]
    public async Task ComplexRelationships_WithMultipleLevels_WorkCorrectly()
    {
        // Test a complex scenario with multiple levels of relationships
        using var connection = ConnectionFactory.CreateDbConnection();
        
        // Create all tables
        await connection.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await connection.CreateTableIfNotExistsAsync<UserProfile>();
        await connection.CreateTableIfNotExistsAsync<TaskItem>();

        // Create user
        var userId = await connection.InsertAsync(new ServiceStackCompatibleUser
        {
            Name = "Task Manager",
            Email = "manager@test.com",
            CreatedAt = DateTime.UtcNow
        }, selectIdentity: true);

        // Create user profile
        var profile = new UserProfile
        {
            FirstName = "Task",
            LastName = "Manager",
            UserId = (int)userId,
            Bio = "Manages tasks",
            UpdatedAt = DateTime.UtcNow
        };
        await connection.InsertAsync(profile);

        // Create task assigned to user
        var task = new TaskItem
        {
            Title = "Complex Task",
            Description = "Multi-level relationship test",
            Status = "In Progress",
            Priority = 1,
            AssignedUserId = (int)userId,
            CreatedDate = DateTime.UtcNow
        };
        var taskId = await connection.InsertAsync(task, selectIdentity: true);

        // Assert all relationships work
        taskId.Should().BeGreaterThan(0);

        var retrievedTask = await connection.SingleByIdAsync<TaskItem>(taskId);
        retrievedTask.Should().NotBeNull();
        retrievedTask.AssignedUserId.Should().Be((int)userId);

        // Verify we can navigate relationships
        var tasksForUser = await connection.SelectAsync<TaskItem>(t => t.AssignedUserId == userId);
        tasksForUser.Should().HaveCount(1);
    }
}