using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoboDodd.OrmLite.Tests;

[Table("test_users")]
[CompositeIndex("IX_User_Email_Active", nameof(Email), nameof(IsActive))]
public class TestUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [Required]
    [StringLength(255)]
    [Index("IX_User_Email", IsUnique = true)]
    public string Email { get; set; } = null!;

    public int Age { get; set; }

    [Default(true)]
    public bool IsActive { get; set; }

    [Default(typeof(DateTime), "CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }

    [CustomField("DECIMAL(10,2)")]
    public decimal Balance { get; set; }

    public string? Notes { get; set; }
}

[Table("test_posts")]
public class TestPost
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    [Index("IX_Post_UserId")]
    public int UserId { get; set; }

    [Default(typeof(DateTime), "CURRENT_TIMESTAMP")]
    public DateTime PublishedAt { get; set; }

    public int ViewCount { get; set; } = 0;
}

[Table("test_categories")]
public class TestCategory
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(200)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}

// Test model for SQL keyword conflicts
[Table("Order")]
public class TestOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("Order")]
    public string OrderName { get; set; } = null!;

    [Column("Date")]
    public DateTime OrderDate { get; set; }

    [Column("Value")]
    public decimal OrderValue { get; set; }

    [Column("User")]
    public string UserName { get; set; } = null!;
}

// Test models for new functionality we added

public class ServiceStackCompatibleUserA : ServiceStackCompatibleUser
{

}
public class ServiceStackCompatibleUserB : ServiceStackCompatibleUser
{

}
// Test model using our custom PrimaryKey and AutoIncrement attributes
public class ServiceStackCompatibleUser
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Test model with Foreign Key CASCADE relationship
public class UserProfile
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    [Required]
    public string FirstName { get; set; } = null!;

    [Required]
    public string LastName { get; set; } = null!;

    [ForeignKey(typeof(ServiceStackCompatibleUser), OnDelete = "CASCADE")]
    public int UserId { get; set; }

    public string? Bio { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Test model with complex relationships
public class BlogPost
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    [Required]
    [Index]
    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    [ForeignKey(typeof(ServiceStackCompatibleUser), OnDelete = "CASCADE")]
    [Index]
    public int AuthorId { get; set; }

    [ForeignKey(typeof(TestCategory), OnDelete = "SET NULL")]
    public Guid? CategoryId { get; set; }

    [Index]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public int ViewCount { get; set; } = 0;

    public bool IsPublished { get; set; } = false;
}

// Test model with composite indexes using our attributes
[CompositeIndex(nameof(Status), nameof(Priority), nameof(CreatedDate))]
[CompositeIndex(nameof(AssignedUserId), nameof(Status), Unique = true)]
public class TaskItem
{
    [PrimaryKey]
    [AutoIncrement]
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public string Status { get; set; } = "New";

    public int Priority { get; set; } = 1;

    [ForeignKey(typeof(ServiceStackCompatibleUser), OnDelete = "SET NULL")]
    public int? AssignedUserId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    [Ignore]
    public string? TempCalculatedField { get; set; }
}
