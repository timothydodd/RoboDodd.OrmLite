using Microsoft.Data.Sqlite;
using RoboDodd.OrmLite;
using System.Data;
using Xunit;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// Integration tests that simulate real-world scenarios similar to those in LogMk2
/// </summary>
public class ServiceStackMigrationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbConnectionFactory _factory;

    public ServiceStackMigrationIntegrationTests()
    {
        var connectionString = "Data Source=:memory:";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _factory = new DbConnectionFactory(connectionString, DatabaseProvider.SQLite);
        
        // Register type handlers for GUIDs
        SharedSqliteConnectionFactory.RegisterTypeHandlers();
    }
    
    /// <summary>
    /// Creates a fresh SQLite connection for tests that need isolated databases
    /// </summary>
    private IDbConnection CreateFreshConnection()
    {
        var tempFile = Path.GetTempFileName();
        var connectionString = $"Data Source={tempFile};";
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        SharedSqliteConnectionFactory.RegisterTypeHandlers();
        return connection;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    [Fact]
    public async Task CompleteWorkflow_CreateTablesInsertDataQueryWithJoins_WorksEndToEnd()
    {
        // Arrange - Create all tables in dependency order
        using var db = CreateFreshConnection();
        
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await db.CreateTableIfNotExistsAsync<TestCategory>();
        await db.CreateTableIfNotExistsAsync<UserProfile>();
        await db.CreateTableIfNotExistsAsync<BlogPost>();
        await db.CreateTableIfNotExistsAsync<TaskItem>();

        // Act & Assert - Insert test data
        var userId = await InsertTestUser(db);
        var categoryId = await InsertTestCategory(db);
        await InsertTestProfile(db, (int)userId);
        var postId = await InsertTestBlogPost(db, (int)userId, categoryId);
        await InsertTestTask(db, (int)userId);

        // Verify data integrity
        await VerifyDataIntegrity(db, (int)userId, categoryId, (int)postId);
    }

    [Fact]
    public async Task DatabaseInitializationPattern_LikeLogMk2_CreatesAllTablesWithIndexes()
    {
        // This test simulates the DatabaseInitializer pattern from LogMk2
        using var db = CreateFreshConnection();

        // Create tables in order (simulating what LogMk2 does)
        var tables = new[]
        {
            typeof(ServiceStackCompatibleUser),
            typeof(TestCategory), 
            typeof(UserProfile),
            typeof(BlogPost),
            typeof(TaskItem)
        };

        // Act - Create all tables
        foreach (var tableType in tables)
        {
            var method = typeof(DapperOrmLiteExtensions)
                .GetMethod(nameof(DapperOrmLiteExtensions.CreateTableIfNotExistsAsync))!
                .MakeGenericMethod(tableType);
            
            var task = (Task<bool>)method.Invoke(null, new object[] { db })!;
            var created = await task;
            Assert.True(created);
        }

        // Verify all tables exist
        foreach (var tableType in tables)
        {
            var method = typeof(DapperOrmLiteExtensions)
                .GetMethod(nameof(DapperOrmLiteExtensions.TableExistsAsync), new[] { typeof(IDbConnection) })!
                .MakeGenericMethod(tableType);
            
            var task = (Task<bool>)method.Invoke(null, new object[] { db })!;
            var exists = await task;
            Assert.True(exists);
        }
    }

    [Fact]
    public async Task RepositoryPattern_WithTransactions_WorksLikeLogMk2Repositories()
    {
        // Simulate the repository pattern used in LogMk2
        using var db = CreateFreshConnection();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await db.CreateTableIfNotExistsAsync<UserProfile>();

        // Act - Simulate repository operations with transaction
        using var transaction = db.BeginTransaction();
        
        try
        {
            // Insert user
            var user = new ServiceStackCompatibleUser
            {
                Name = "Transaction Test User",
                Email = "transaction@test.com"
            };
            var userId = await db.InsertAsync(user, selectIdentity: true);
            
            // Insert profile
            var profile = new UserProfile
            {
                FirstName = "Transaction",
                LastName = "User",
                UserId = (int)userId,
                Bio = "Test transaction"
            };
            await db.InsertAsync(profile);
            
            transaction.Commit();
            
            // Assert - Both records should exist
            var userExists = await db.ExistsAsync<ServiceStackCompatibleUser>(userId);
            var profileCount = await db.CountAsync<UserProfile>(p => p.UserId == userId);
            
            Assert.True(userExists);
            Assert.Equal(1, profileCount);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    [Fact]
    public async Task PaginationAndFiltering_LikeLogMkLogRepo_WorksCorrectly()
    {
        // Simulate the pagination pattern used in LogMk2's LogRepo
        using var db = CreateFreshConnection();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await db.CreateTableIfNotExistsAsync<TestCategory>();
        await db.CreateTableIfNotExistsAsync<BlogPost>();

        // Insert test data
        var userId = await InsertTestUser(db);
        
        var posts = new List<BlogPost>();
        for (int i = 1; i <= 10; i++)
        {
            var post = new BlogPost
            {
                Title = $"Post {i}",
                Content = $"Content for post {i}",
                AuthorId = (int)userId,
                IsPublished = i % 2 == 0, // Every other post is published
                ViewCount = i * 10
            };
            await db.InsertAsync(post);
            posts.Add(post);
        }

        // Act - Test pagination (simulating LogRepo.GetLogsAsync pattern)
        var publishedPosts = await db.SelectAsync<BlogPost>(
            p => p.IsPublished == true, 
            skip: 2, 
            take: 3
        );

        // Assert
        Assert.Equal(3, publishedPosts.Count);
        
        // Test filtering by author
        var authorPosts = await db.SelectAsync<BlogPost>(p => p.AuthorId == userId);
        Assert.Equal(10, authorPosts.Count);
    }

    [Fact]
    public async Task BulkOperations_LikeLogMkWorkQueueRepo_HandleBatchInserts()
    {
        // Simulate bulk operations like in LogMk2's WorkQueueRepo
        using var db = CreateFreshConnection();
        await db.CreateTableIfNotExistsAsync<TaskItem>();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        var userId = await InsertTestUser(db);

        // Act - Bulk insert tasks
        var tasks = new List<TaskItem>();
        for (int i = 1; i <= 50; i++)
        {
            tasks.Add(new TaskItem
            {
                Title = $"Task {i}",
                Description = $"Description for task {i}",
                Status = i % 3 == 0 ? "Completed" : "Pending",
                Priority = (i % 5) + 1,
                AssignedUserId = (int)userId
            });
        }

        // Insert in batches (simulating WorkQueueRepo batch processing)
        const int batchSize = 10;
        for (int i = 0; i < tasks.Count; i += batchSize)
        {
            var batch = tasks.Skip(i).Take(batchSize).ToList();
            foreach (var task in batch)
            {
                await db.InsertAsync(task);
            }
        }

        // Assert - Verify all tasks were inserted
        var totalTasks = await db.CountAsync<TaskItem>();
        Assert.Equal(50, totalTasks);

        var completedTasks = await db.CountAsync<TaskItem>(t => t.Status == "Completed");
        var pendingTasks = await db.CountAsync<TaskItem>(t => t.Status == "Pending");
        
        Assert.True(completedTasks > 0);
        Assert.True(pendingTasks > 0);
        Assert.Equal(50, completedTasks + pendingTasks);
    }

    [Fact]
    public async Task HealthCheckPattern_LikeLogMkHealthCheck_ValidatesDatabase()
    {
        // Simulate the health check pattern from LogMk2
        using var db = CreateFreshConnection();
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();

        // Act - Simulate health check query
        try
        {
            var count = await db.CountAsync<ServiceStackCompatibleUser>();
            var healthCheckPassed = true;
            
            // Assert
            Assert.True(healthCheckPassed);
            Assert.True(count >= 0); // Just verify query executes
        }
        catch
        {
            Assert.True(false, "Health check should not fail");
        }
    }

    [Fact]
    public async Task ErrorHandling_WithInvalidOperations_HandlesGracefully()
    {
        using var db = CreateFreshConnection();
        
        // Test operations on non-existent table
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(async () =>
        {
            await db.CountAsync<ServiceStackCompatibleUser>();
        });

        // Test invalid foreign key insert (after creating tables)
        await db.CreateTableIfNotExistsAsync<ServiceStackCompatibleUser>();
        await db.CreateTableIfNotExistsAsync<UserProfile>();

        var invalidProfile = new UserProfile
        {
            FirstName = "Invalid",
            LastName = "User",
            UserId = 999999 // Non-existent user ID
        };

        // Foreign key constraints are enforced in file-based SQLite, so this should fail
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(async () =>
        {
            await db.InsertAsync(invalidProfile);
        });
    }

    // Helper methods

    private async Task<long> InsertTestUser(IDbConnection db)
    {
        var user = new ServiceStackCompatibleUser
        {
            Name = "Test User",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        return await db.InsertAsync(user, selectIdentity: true);
    }

    private async Task<Guid> InsertTestCategory(IDbConnection db)
    {
        var category = new TestCategory
        {
            Id = Guid.NewGuid(),
            Name = "Technology",
            Description = "Technology related posts",
            CreatedAt = DateTime.UtcNow
        };
        await db.InsertAsync(category);
        return category.Id;
    }

    private async Task InsertTestProfile(IDbConnection db, int userId)
    {
        var profile = new UserProfile
        {
            FirstName = "Test",
            LastName = "User",
            UserId = userId,
            Bio = "Test user profile",
            UpdatedAt = DateTime.UtcNow
        };
        await db.InsertAsync(profile);
    }

    private async Task<long> InsertTestBlogPost(IDbConnection db, int authorId, Guid categoryId)
    {
        var post = new BlogPost
        {
            Title = "Test Blog Post",
            Content = "This is a test blog post content",
            AuthorId = authorId,
            CategoryId = categoryId,
            IsPublished = true,
            ViewCount = 0,
            PublishedAt = DateTime.UtcNow
        };
        return await db.InsertAsync(post, selectIdentity: true);
    }

    private async Task InsertTestTask(IDbConnection db, int assignedUserId)
    {
        var task = new TaskItem
        {
            Title = "Test Task",
            Description = "This is a test task",
            Status = "New",
            Priority = 1,
            AssignedUserId = assignedUserId,
            CreatedDate = DateTime.UtcNow
        };
        await db.InsertAsync(task);
    }

    private async Task VerifyDataIntegrity(IDbConnection db, int userId, Guid categoryId, int postId)
    {
        // Verify user exists
        var user = await db.SingleByIdAsync<ServiceStackCompatibleUser>(userId);
        Assert.NotNull(user);

        // Verify profile exists for user
        var profile = await db.SingleAsync<UserProfile>(p => p.UserId == userId);
        Assert.NotNull(profile);

        // Verify category exists
        var category = await db.SingleByIdAsync<TestCategory>(categoryId);
        Assert.NotNull(category);

        // Verify blog post exists and has correct relationships
        var post = await db.SingleByIdAsync<BlogPost>(postId);
        Assert.NotNull(post);
        Assert.Equal(userId, post.AuthorId);
        Assert.Equal(categoryId, post.CategoryId);

        // Verify task exists for user
        var task = await db.SingleAsync<TaskItem>(t => t.AssignedUserId == userId);
        Assert.NotNull(task);
        Assert.Equal(userId, task.AssignedUserId);
    }
}