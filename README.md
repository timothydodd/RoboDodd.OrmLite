# RoboDodd.OrmLite

A lightweight, high-performance ORM library for .NET 9+ that provides ServiceStack OrmLite-compatible API using Dapper underneath. Offers the simplicity of Dapper with the convenience of a full-featured ORM.

## Features

- 🚀 **High Performance** - Built on Dapper for maximum speed
- 🔄 **ServiceStack OrmLite Compatible** - Drop-in replacement API
- 🗄️ **Multi-Database** - SQLite and MySQL support with automatic dialect detection  
- 🧪 **Fully Tested** - 92 comprehensive tests with both SQLite and MySQL
- 💾 **Rich CRUD Operations** - Complete set of Create, Read, Update, Delete operations
- 🔍 **LINQ Support** - Expression-based queries with proper SQL generation
- 📦 **Bulk Operations** - Efficient bulk insert, update, and delete
- 🔢 **Pagination** - Built-in skip/take support for large datasets
- 🆔 **Smart Save** - Automatic insert vs update detection
- 🏷️ **Attributes** - Custom attributes for indexes, constraints, and field types

## Quick Start

### Installation

```bash
# Add the project reference
dotnet add reference path/to/RoboDodd.OrmLite/RoboDodd.OrmLite.csproj
```

### Basic Usage

```csharp
using RoboDodd.OrmLite;
using System.ComponentModel.DataAnnotations;

// Define your model
[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; }
    
    [Index("IX_User_Email", IsUnique = true)]
    public string Email { get; set; }
    
    public DateTime Created { get; set; }
}

// Setup connection factory
var connectionFactory = new DbConnectionFactory(connectionString);

// Use the extensions
using var connection = connectionFactory.CreateDbConnection();
await connection.OpenAsync();

// Create table
await connection.CreateTableIfNotExistsAsync<User>();

// Basic CRUD
var user = new User { Name = "John", Email = "john@example.com", Created = DateTime.UtcNow };

// Insert
var id = await connection.InsertAsync(user, selectIdentity: true);

// Select
var users = await connection.SelectAsync<User>();
var userById = await connection.SingleByIdAsync<User>(id);
var userByEmail = await connection.SingleAsync<User>(u => u.Email == "john@example.com");

// Update  
user.Name = "John Doe";
await connection.UpdateAsync(user);

// Delete
await connection.DeleteAsync(user);
```

## Advanced Features

### Bulk Operations

```csharp
// Bulk insert
var users = new[] {
    new User { Name = "User1", Email = "user1@example.com" },
    new User { Name = "User2", Email = "user2@example.com" }
};
await connection.InsertAllAsync(users);

// Bulk update
await connection.UpdateAllAsync(users);

// Bulk delete
await connection.DeleteAllAsync(users);
```

### Count and Exists Operations

```csharp
// Count records
var totalUsers = await connection.CountAsync<User>();
var activeUsers = await connection.CountAsync<User>(u => u.IsActive);

// Check existence
var exists = await connection.ExistsAsync<User>(u => u.Email == "john@example.com");
var existsById = await connection.ExistsAsync<User>(123);
```

### Pagination

```csharp
// Get page 2 with 10 users per page
var page2 = await connection.SelectAsync<User>(10, 10); // skip 10, take 10

// Filtered pagination
var activePage = await connection.SelectAsync<User>(
    u => u.IsActive, 
    skip: 20, 
    take: 10
);
```

### Smart Save (Upsert)

```csharp
var user = new User { Name = "John", Email = "john@example.com" };

// Will insert if new, update if exists
await connection.SaveAsync(user); // Inserts and sets the ID

user.Name = "John Updated";
await connection.SaveAsync(user); // Updates the existing record
```

### LINQ Expression Support

```csharp
// Supported string methods
var users = await connection.SelectAsync<User>(u => u.Name.StartsWith("John"));
var users2 = await connection.SelectAsync<User>(u => u.Email.Contains("@gmail"));
var users3 = await connection.SelectAsync<User>(u => u.Name.EndsWith("Doe"));

// Comparison operators
var adults = await connection.SelectAsync<User>(u => u.Age >= 18);
var recent = await connection.SelectAsync<User>(u => u.Created > DateTime.Today);
```

## Database Support

### SQLite
```csharp
var connectionFactory = new DbConnectionFactory("Data Source=app.db;");
```

### MySQL
```csharp
var connectionFactory = new DbConnectionFactory(
    "Server=localhost;Database=mydb;User=user;Password=pass;"
);
```

## Custom Attributes

### Field Customization
```csharp
public class Product
{
    [CustomField("DECIMAL(10,2)")]
    public decimal Price { get; set; }
    
    [Default(typeof(DateTime), "CURRENT_TIMESTAMP")]
    public DateTime Created { get; set; }
}
```

### Indexes
```csharp
[CompositeIndex("IX_User_Email_Status", nameof(Email), nameof(Status))]
public class User
{
    [Index("IX_User_Email", IsUnique = true)]
    public string Email { get; set; }
    
    public UserStatus Status { get; set; }
}
```

## API Reference

### Core Operations
- `SelectAsync<T>()` - Get all records
- `SelectAsync<T>(predicate)` - Get filtered records  
- `SingleByIdAsync<T>(id)` - Get single record by ID
- `SingleAsync<T>(predicate)` - Get single record by condition
- `FirstOrDefaultAsync<T>()` - Get first record or null
- `InsertAsync<T>(entity)` - Insert record
- `UpdateAsync<T>(entity)` - Update record
- `DeleteAsync<T>(entity)` - Delete record
- `SaveAsync<T>(entity)` - Insert or update intelligently

### Bulk Operations
- `InsertAllAsync<T>(entities)` - Bulk insert
- `UpdateAllAsync<T>(entities)` - Bulk update
- `DeleteAllAsync<T>(entities)` - Bulk delete

### Query Operations
- `CountAsync<T>()` - Count all records
- `CountAsync<T>(predicate)` - Count filtered records
- `ExistsAsync<T>(predicate)` - Check if records exist
- `ExistsAsync<T>(id)` - Check if record exists by ID

### Schema Operations
- `CreateTableIfNotExistsAsync<T>()` - Create table with indexes
- `TableExistsAsync<T>()` - Check if table exists

## Requirements

- **.NET 9.0+**
- **Dapper 2.1.66+**
- **Microsoft.Data.Sqlite 9.0.8+** (for SQLite)
- **MySql.Data 9.4.0+** (for MySQL)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

MIT License - see LICENSE file for details.

## Changelog

### v1.0.0
- Initial release with complete CRUD operations
- SQLite and MySQL support  
- LINQ expression support
- Comprehensive test suite with 92+ tests
- ServiceStack OrmLite compatible API