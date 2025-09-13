using System.Data;
using Dapper;

namespace RoboDodd.OrmLite
{
    /// <summary>
    /// Simple connection factory interface to replace ServiceStack's IDbConnectionFactory
    /// </summary>
    public interface IDbConnectionFactory
    {
        IDbConnection CreateDbConnection();
    }

    public enum DatabaseProvider
    {
        SQLite,
        MySql
    }

    /// <summary>
    /// Implementation of connection factory for SQLite and MySQL
    /// </summary>
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        private readonly DatabaseProvider? _provider;
        private static bool _typeHandlersRegistered = false;

        public DbConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
            RegisterTypeHandlers();
        }

        public DbConnectionFactory(string connectionString, DatabaseProvider provider)
        {
            _connectionString = connectionString;
            _provider = provider;
            RegisterTypeHandlers();
        }

        public IDbConnection CreateConnection() => CreateDbConnection();
        
        public IDbConnection CreateDbConnection()
        {
            // Use explicit provider if specified
            if (_provider.HasValue)
            {
                return _provider.Value switch
                {
                    DatabaseProvider.SQLite => new Microsoft.Data.Sqlite.SqliteConnection(_connectionString),
                    DatabaseProvider.MySql => new MySql.Data.MySqlClient.MySqlConnection(_connectionString),
                    _ => throw new NotSupportedException($"Database provider {_provider} is not supported")
                };
            }
            
            // Auto-detect database type based on connection string
            if (_connectionString.Contains("Data Source"))
            {
                return new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            }
            else
            {
                return new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
            }
        }

        private static void RegisterTypeHandlers()
        {
            if (!_typeHandlersRegistered)
            {
                // Register GUID type handler for SQLite
                SqlMapper.AddTypeHandler(new GuidTypeHandler());
                SqlMapper.AddTypeHandler(new NullableGuidTypeHandler());
                _typeHandlersRegistered = true;
            }
        }
    }

    /// <summary>
    /// Type handler for GUIDs in SQLite
    /// </summary>
    public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override Guid Parse(object value)
        {
            return value switch
            {
                string s => Guid.Parse(s),
                Guid g => g,
                _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to Guid")
            };
        }

        public override void SetValue(System.Data.IDbDataParameter parameter, Guid value)
        {
            parameter.Value = value.ToString();
        }
    }

    /// <summary>
    /// Type handler for nullable GUIDs in SQLite
    /// </summary>
    public class NullableGuidTypeHandler : SqlMapper.TypeHandler<Guid?>
    {
        public override Guid? Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            return value switch
            {
                string s => string.IsNullOrEmpty(s) ? null : Guid.Parse(s),
                Guid g => g,
                _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to Guid?")
            };
        }

        public override void SetValue(System.Data.IDbDataParameter parameter, Guid? value)
        {
            parameter.Value = value?.ToString() ?? (object)DBNull.Value;
        }
    }
}