using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RoboDodd.OrmLite;

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