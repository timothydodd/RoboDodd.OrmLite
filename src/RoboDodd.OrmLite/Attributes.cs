using System.ComponentModel.DataAnnotations;

namespace RoboDodd.OrmLite
{
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
        public string Name { get; }
        public string[] Properties { get; }
        public bool IsUnique { get; set; }
        
        public CompositeIndexAttribute(string name, params string[] properties)
        {
            Name = name;
            Properties = properties;
        }
        
        public CompositeIndexAttribute(string name, bool isUnique, params string[] properties)
        {
            Name = name;
            Properties = properties;
            IsUnique = isUnique;
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
}