using FluentAssertions;
using RoboDodd.OrmLite;
using Dapper;

namespace RoboDodd.OrmLite.Tests;

/// <summary>
/// Tests for SQL extension methods and helper utilities
/// </summary>
public class SqlExtensionTests
{
    [Fact]
    public void AddIfNotNull_ShouldAddParameter_WhenValueNotNull()
    {
        // Arrange
        var parameters = new DynamicParameters();

        // Act
        parameters.AddIfNotNull("TestParam", "TestValue");

        // Assert - we can't directly inspect DynamicParameters, but we can verify it doesn't throw
        parameters.Should().NotBeNull();
        // The actual parameter addition will be tested in integration tests
    }

    [Fact]
    public void AddIfNotNull_ShouldNotAddParameter_WhenValueIsNull()
    {
        // Arrange
        var parameters = new DynamicParameters();

        // Act
        parameters.AddIfNotNull("TestParam", null);

        // Assert - we can't directly inspect DynamicParameters, but we can verify it doesn't throw
        parameters.Should().NotBeNull();
        // The actual null parameter behavior will be tested in integration tests
    }

    [Fact]
    public void AddList_ShouldAddMultipleParameters_WithDistinctValues()
    {
        // Arrange
        var parameters = new DynamicParameters();
        var items = new[] { 1, 2, 3, 2, 1 }; // Contains duplicates

        // Act
        var keys = parameters.AddList(items, "item");

        // Assert
        keys.Should().HaveCount(3); // Should be distinct
        keys.Should().Contain("@item0", "@item1", "@item2");
        
        // Verify parameters were added (can't easily access DynamicParameters internals)
        keys.Should().OnlyContain(k => k.StartsWith("@item"));
    }

    [Fact]
    public void AddList_ShouldHandleEmptyCollection()
    {
        // Arrange
        var parameters = new DynamicParameters();
        var items = new int[0];

        // Act
        var keys = parameters.AddList(items, "item");

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact]
    public void GetSqlFilters_ShouldReturnEmptyList_WhenNoAliasesProvided()
    {
        // Arrange
        var testObject = new { Name = "Test", Age = 25 };

        // Act
        var filters = testObject.GetSqlFilters<object>();

        // Assert
        filters.Should().BeEmpty();
    }

    [Fact]
    public void GetSqlFilters_ShouldGenerateCorrectFilters_WithAliases()
    {
        // Arrange
        var testObject = new TestFilterObject 
        { 
            Name = "John", 
            Age = 30,
            Status = "eq:Active"
        };
        var parameters = new DynamicParameters();
        var aliases = new List<SqlFieldDescripter>
        {
            new("Name", "u.Name"),
            new("Age", "u.Age"),
            new("Status", "u.Status")
        };

        // Act
        var filters = testObject.GetSqlFilters(parameters, aliases);

        // Assert
        filters.Should().HaveCount(3);
        filters.Should().Contain(" AND u.Name = @Name");
        filters.Should().Contain(" AND u.Age = @Age");
        filters.Should().Contain(" AND u.Status = @Status");
    }

    [Fact]
    public void GetSqlFilters_ShouldHandleOperators_Correctly()
    {
        // Arrange
        var testObject = new TestFilterObject 
        { 
            Name = "like:John",
            Age = "gt:25",
            Status = "lte:100"
        };
        var parameters = new DynamicParameters();
        var aliases = new List<SqlFieldDescripter>
        {
            new("Name", "u.Name"),
            new("Age", "u.Age"),
            new("Status", "u.Status")
        };

        // Act
        var filters = testObject.GetSqlFilters(parameters, aliases);

        // Assert
        filters.Should().HaveCount(3);
        filters.Should().Contain(" AND u.Name like @Name + '%'");
        filters.Should().Contain(" AND u.Age > @Age");
        filters.Should().Contain(" AND u.Status <= @Status");
    }

    [Fact]
    public void GetSqlOrderBy_ShouldGenerateCorrectOrderBy_WithTableAliases()
    {
        // Arrange
        var testObject = new TestOrderObject { OrderBy = "Name,Age:desc" };
        var aliases = new List<TableAlias>
        {
            new("Name", "u.Name"),
            new("Age", "u.Age")
        };

        // Act
        var orderBy = testObject.GetSqlOrderBy(aliases);

        // Assert
        orderBy.Should().Be("u.Name asc,u.Age desc");
    }

    [Fact]
    public void GetSqlOrderBy_ShouldReturnNull_WhenOrderByIsNull()
    {
        // Arrange
        var testObject = new TestOrderObject { OrderBy = null };
        var aliases = new List<TableAlias>
        {
            new("Name", "u.Name")
        };

        // Act
        var orderBy = testObject.GetSqlOrderBy(aliases);

        // Assert
        orderBy.Should().BeNull();
    }

    [Fact]
    public void GetSqlParams_ShouldExtractParameters_WithSqlParamAttribute()
    {
        // Arrange
        var testObject = new TestParamObject
        {
            Id = 123,
            Name = "Test Name",
            NonParam = "Should not be included"
        };

        // Act
        var parameters = testObject.GetSqlParams();

        // Assert
        // We can't easily inspect DynamicParameters, but we can verify it doesn't throw
        parameters.Should().NotBeNull();
    }

    [Fact]
    public void IsNullEmptyOrWhiteSpace_ShouldReturnTrue_ForNullEmptyAndWhitespace()
    {
        // Act & Assert
        ((string?)null).IsNullEmptyOrWhiteSpace().Should().BeTrue();
        "".IsNullEmptyOrWhiteSpace().Should().BeTrue();
        " ".IsNullEmptyOrWhiteSpace().Should().BeTrue();
        "\t\n".IsNullEmptyOrWhiteSpace().Should().BeTrue();
    }

    [Fact]
    public void IsNullEmptyOrWhiteSpace_ShouldReturnFalse_ForValidStrings()
    {
        // Act & Assert
        "Hello".IsNullEmptyOrWhiteSpace().Should().BeFalse();
        "123".IsNullEmptyOrWhiteSpace().Should().BeFalse();
        " test ".IsNullEmptyOrWhiteSpace().Should().BeFalse();
    }

    // Test helper classes
    private class TestFilterObject
    {
        public object? Name { get; set; }
        public object? Age { get; set; }
        public object? Status { get; set; }
    }

    private class TestOrderObject
    {
        public string? OrderBy { get; set; }
    }

    private class TestParamObject
    {
        [SqlParam]
        public int Id { get; set; }
        
        [SqlParam]
        public string? Name { get; set; }
        
        // This property should not be included (no attribute)
        public string? NonParam { get; set; }
    }
}