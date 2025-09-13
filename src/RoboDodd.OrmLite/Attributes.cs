using System.ComponentModel.DataAnnotations;

namespace RoboDodd.OrmLite
{
    /// <summary>
    /// Attribute to mark a property as a primary key
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class PrimaryKeyAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute to mark a property as auto-incrementing
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class AutoIncrementAttribute : Attribute
    {
    }

    /// <summary>
    /// Attribute to mark a property to be ignored by the ORM
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class IgnoreAttribute : Attribute
    {
    }
    /// <summary>
    /// Attribute to mark a property as needing a database index
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class IndexAttribute : Attribute
    {
        public string? Name { get; set; }
        public bool IsUnique { get; set; }
        
        public IndexAttribute() { }
        
        public IndexAttribute(string name)
        {
            Name = name;
        }
        
        public IndexAttribute(bool isUnique)
        {
            IsUnique = isUnique;
        }
        
        public IndexAttribute(string name, bool isUnique)
        {
            Name = name;
            IsUnique = isUnique;
        }
    }

    /// <summary>
    /// Attribute to mark multiple properties as part of a composite index
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CompositeIndexAttribute : Attribute
    {
        public string Name { get; set; }
        public string[] FieldNames { get; }
        public bool Unique { get; set; }
        
        public CompositeIndexAttribute(params string[] fieldNames)
        {
            FieldNames = fieldNames;
            Name = string.Join("_", fieldNames) + "_idx";
        }
    }

    /// <summary>
    /// Attribute to specify custom field types for database columns
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CustomFieldAttribute : Attribute
    {
        public string FieldType { get; }
        
        public CustomFieldAttribute(string fieldType)
        {
            FieldType = fieldType;
        }
    }

    /// <summary>
    /// Attribute to specify default values for database columns
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DefaultAttribute : Attribute
    {
        public object? Value { get; }
        public Type? Type { get; }
        public string? Expression { get; }
        
        public DefaultAttribute(object value)
        {
            Value = value;
        }
        
        public DefaultAttribute(Type type, string expression)
        {
            Type = type;
            Expression = expression;
        }
    }

    /// <summary>
    /// ServiceStack-compatible ForeignKey attribute with cascade options
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ReferenceAttribute : Attribute
    {
        public Type? ForeignType { get; }
        public string? ForeignTableName { get; }
        public string? OnDelete { get; set; }
        public string? OnUpdate { get; set; }
        
        public ReferenceAttribute(Type foreignType)
        {
            ForeignType = foreignType;
        }
        
        public ReferenceAttribute(string foreignTableName)
        {
            ForeignTableName = foreignTableName;
        }
        
        public ReferenceAttribute(Type foreignType, string onDelete)
        {
            ForeignType = foreignType;
            OnDelete = onDelete;
        }
    }

    /// <summary>
    /// Alias for ServiceStack ForeignKey with cascade options
    /// </summary>
    public class ForeignKeyAttribute : ReferenceAttribute
    {
        public ForeignKeyAttribute(Type foreignType) : base(foreignType) { }
        public ForeignKeyAttribute(Type foreignType, string onDelete) : base(foreignType, onDelete) { }
    }
}