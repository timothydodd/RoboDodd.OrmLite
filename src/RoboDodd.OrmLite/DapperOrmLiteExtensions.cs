using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Dapper;

namespace RoboDodd.OrmLite
{
    /// <summary>
    /// Extension methods that mimic ServiceStack OrmLite API but use Dapper underneath
    /// This allows existing code to work with minimal changes
    /// </summary>
    public static class DapperOrmLiteExtensions
    {
        #region Select Operations

        public static async Task<List<T>> SelectAsync<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var sql = $"SELECT * FROM {escapedTableName}";
            var result = await connection.QueryAsync<T>(sql);
            return result.ToList();
        }

        public static async Task<List<T>> SelectAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var (whereClause, parameters) = BuildWhereClause(predicate, isMySql);
            var sql = $"SELECT * FROM {escapedTableName} WHERE {whereClause}";
            var result = await connection.QueryAsync<T>(sql, parameters);
            return result.ToList();
        }

        public static async Task<T?> SingleByIdAsync<T>(this IDbConnection connection, object id)
        {
            var tableName = GetTableName<T>();
            var idColumn = GetIdColumnName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var escapedIdColumn = EscapeColumnName(idColumn, isMySql);
            var sql = $"SELECT * FROM {escapedTableName} WHERE {escapedIdColumn} = @Id";
            return await connection.QuerySingleOrDefaultAsync<T>(sql, new { Id = id });
        }

        public static async Task<T?> SingleAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var (whereClause, parameters) = BuildWhereClause(predicate, isMySql);
            var sql = $"SELECT * FROM {escapedTableName} WHERE {whereClause} LIMIT 1";
            return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
        }

        #endregion

        #region Count Operations

        public static async Task<long> CountAsync<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var sql = $"SELECT COUNT(*) FROM {escapedTableName}";
            return await connection.QuerySingleAsync<long>(sql);
        }

        public static async Task<long> CountAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var (whereClause, parameters) = BuildWhereClause(predicate, isMySql);
            var sql = $"SELECT COUNT(*) FROM {escapedTableName} WHERE {whereClause}";
            return await connection.QuerySingleAsync<long>(sql, parameters);
        }

        #endregion

        #region Exists Operations

        public static async Task<bool> ExistsAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
        {
            var count = await connection.CountAsync(predicate);
            return count > 0;
        }

        public static async Task<bool> ExistsAsync<T>(this IDbConnection connection, object id)
        {
            var tableName = GetTableName<T>();
            var idColumn = GetIdColumnName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var escapedIdColumn = EscapeColumnName(idColumn, isMySql);
            var sql = $"SELECT COUNT(*) FROM {escapedTableName} WHERE {escapedIdColumn} = @Id";
            var count = await connection.QuerySingleAsync<long>(sql, new { Id = id });
            return count > 0;
        }

        #endregion

        #region First/FirstOrDefault Operations

        public static async Task<T?> FirstAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var (whereClause, parameters) = BuildWhereClause(predicate, isMySql);
            var sql = $"SELECT * FROM {escapedTableName} WHERE {whereClause} LIMIT 1";
            var result = await connection.QueryAsync<T>(sql, parameters);
            var first = result.FirstOrDefault();
            if (first == null)
                throw new InvalidOperationException("Sequence contains no elements");
            return first;
        }

        public static async Task<T?> FirstOrDefaultAsync<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var sql = $"SELECT * FROM {escapedTableName} LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<T>(sql);
        }

        public static async Task<T?> FirstOrDefaultAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var (whereClause, parameters) = BuildWhereClause(predicate, isMySql);
            var sql = $"SELECT * FROM {escapedTableName} WHERE {whereClause} LIMIT 1";
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }

        #endregion

        #region Insert Operations

        public static async Task<long> InsertAsync<T>(this IDbConnection connection, T entity, bool selectIdentity = false)
        {
            var tableName = GetTableName<T>();
            var type = typeof(T);
            var properties = GetInsertProperties<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");

            var escapedTableName = EscapeTableName(tableName, isMySql);
            var columnNames = string.Join(", ", properties.Select(p => EscapeColumnName(p.Name, isMySql)));
            var parameterNames = string.Join(", ", properties.Select(p => "@" + p.Name));

            var sql = $"INSERT INTO {escapedTableName} ({columnNames}) VALUES ({parameterNames})";

            if (selectIdentity)
            {
                sql += isMySql ? "; SELECT LAST_INSERT_ID()" : "; SELECT last_insert_rowid()";
                return await connection.QuerySingleAsync<long>(sql, entity);
            }
            else
            {
                await connection.ExecuteAsync(sql, entity);
                return 0;
            }
        }

        public static async Task<int> InsertAllAsync<T>(this IDbConnection connection, IEnumerable<T> entities)
        {
            var entitiesList = entities.ToList();
            if (!entitiesList.Any())
                return 0;

            var tableName = GetTableName<T>();
            var properties = GetInsertProperties<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");

            var escapedTableName = EscapeTableName(tableName, isMySql);
            var columnNames = string.Join(", ", properties.Select(p => EscapeColumnName(p.Name, isMySql)));
            var parameterNames = string.Join(", ", properties.Select(p => "@" + p.Name));

            var sql = $"INSERT INTO {escapedTableName} ({columnNames}) VALUES ({parameterNames})";

            return await connection.ExecuteAsync(sql, entitiesList);
        }

        #endregion

        #region Bulk Operations

        public static async Task<int> UpdateAllAsync<T>(this IDbConnection connection, IEnumerable<T> entities)
        {
            var entitiesList = entities.ToList();
            if (!entitiesList.Any())
                return 0;

            var tableName = GetTableName<T>();
            var idColumn = GetIdColumnName<T>();
            var properties = GetUpdateProperties<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");

            var escapedTableName = EscapeTableName(tableName, isMySql);
            var escapedIdColumn = EscapeColumnName(idColumn, isMySql);
            var setClause = string.Join(", ", properties.Select(p => $"{EscapeColumnName(p.Name, isMySql)} = @{p.Name}"));
            var sql = $"UPDATE {escapedTableName} SET {setClause} WHERE {escapedIdColumn} = @{idColumn}";

            return await connection.ExecuteAsync(sql, entitiesList);
        }

        public static async Task<int> DeleteAllAsync<T>(this IDbConnection connection, IEnumerable<T> entities)
        {
            var entitiesList = entities.ToList();
            if (!entitiesList.Any())
                return 0;

            var tableName = GetTableName<T>();
            var idColumn = GetIdColumnName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");

            var escapedTableName = EscapeTableName(tableName, isMySql);
            var escapedIdColumn = EscapeColumnName(idColumn, isMySql);
            var sql = $"DELETE FROM {escapedTableName} WHERE {escapedIdColumn} = @{idColumn}";

            return await connection.ExecuteAsync(sql, entitiesList);
        }

        #endregion

        #region Pagination Operations

        public static async Task<List<T>> SelectAsync<T>(this IDbConnection connection, int skip, int take)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            
            string sql;
            if (isMySql)
            {
                sql = $"SELECT * FROM {escapedTableName} LIMIT @Take OFFSET @Skip";
            }
            else
            {
                sql = $"SELECT * FROM {escapedTableName} LIMIT @Take OFFSET @Skip";
            }
            
            var result = await connection.QueryAsync<T>(sql, new { Skip = skip, Take = take });
            return result.ToList();
        }

        public static async Task<List<T>> SelectAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate, int skip, int take)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var (whereClause, parameters) = BuildWhereClause(predicate, isMySql);
            
            string sql;
            if (isMySql)
            {
                sql = $"SELECT * FROM {escapedTableName} WHERE {whereClause} LIMIT @Take OFFSET @Skip";
            }
            else
            {
                sql = $"SELECT * FROM {escapedTableName} WHERE {whereClause} LIMIT @Take OFFSET @Skip";
            }
            
            var dynamicParams = new DynamicParameters();
            dynamicParams.AddDynamicParams(parameters);
            dynamicParams.Add("Skip", skip);
            dynamicParams.Add("Take", take);
            
            var result = await connection.QueryAsync<T>(sql, dynamicParams);
            return result.ToList();
        }

        #endregion

        #region Upsert Operations

        public static async Task<int> SaveAsync<T>(this IDbConnection connection, T entity)
        {
            var idProperty = typeof(T).GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                ?? typeof(T).GetProperty("Id");

            if (idProperty == null)
                throw new InvalidOperationException($"No key property found for type {typeof(T).Name}");

            var idValue = idProperty.GetValue(entity);
            var isNewEntity = IsDefaultValue(idValue, idProperty.PropertyType);

            if (isNewEntity)
            {
                var insertResult = await connection.InsertAsync(entity, selectIdentity: true);
                if (insertResult > 0 && idProperty.PropertyType == typeof(int))
                {
                    idProperty.SetValue(entity, (int)insertResult);
                }
                return insertResult > 0 ? 1 : 0;
            }
            else
            {
                return await connection.UpdateAsync(entity);
            }
        }

        private static bool IsDefaultValue(object? value, Type type)
        {
            if (value == null)
                return true;
                
            if (type == typeof(int) || type == typeof(int?))
                return Equals(value, 0);
                
            if (type == typeof(long) || type == typeof(long?))
                return Equals(value, 0L);
                
            if (type == typeof(Guid) || type == typeof(Guid?))
                return Equals(value, Guid.Empty);
                
            if (type.IsValueType)
                return value.Equals(Activator.CreateInstance(type));
                
            return false;
        }

        #endregion

        #region Update Operations

        public static async Task<int> UpdateAsync<T>(this IDbConnection connection, T entity)
        {
            var tableName = GetTableName<T>();
            var idColumn = GetIdColumnName<T>();
            var properties = GetUpdateProperties<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");

            var escapedTableName = EscapeTableName(tableName, isMySql);
            var escapedIdColumn = EscapeColumnName(idColumn, isMySql);
            var setClause = string.Join(", ", properties.Select(p => $"{EscapeColumnName(p.Name, isMySql)} = @{p.Name}"));
            var sql = $"UPDATE {escapedTableName} SET {setClause} WHERE {escapedIdColumn} = @{idColumn}";

            return await connection.ExecuteAsync(sql, entity);
        }

        public static async Task<int> UpdateOnlyAsync<T>(this IDbConnection connection,
            Expression<Func<T>> updateFields, Expression<Func<T, bool>> where)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var (setClause, setParameters) = BuildUpdateClause(updateFields, isMySql);
            var (whereClause, whereParameters) = BuildWhereClause(where, isMySql);

            var parameters = new DynamicParameters();
            parameters.AddDynamicParams(setParameters);
            parameters.AddDynamicParams(whereParameters);

            var escapedTableName = EscapeTableName(tableName, isMySql);
            var sql = $"UPDATE {escapedTableName} SET {setClause} WHERE {whereClause}";
            return await connection.ExecuteAsync(sql, parameters);
        }

        #endregion

        #region Delete Operations

        public static async Task<int> DeleteAsync<T>(this IDbConnection connection, T entity)
        {
            var tableName = GetTableName<T>();
            var idColumn = GetIdColumnName<T>();
            var idProperty = typeof(T).GetProperty(idColumn);
            var idValue = idProperty?.GetValue(entity);
            var isMySql = connection.GetType().Name.Contains("MySql");

            var escapedTableName = EscapeTableName(tableName, isMySql);
            var escapedIdColumn = EscapeColumnName(idColumn, isMySql);
            var sql = $"DELETE FROM {escapedTableName} WHERE {escapedIdColumn} = @Id";
            return await connection.ExecuteAsync(sql, new { Id = idValue });
        }

        public static async Task<int> DeleteAsync<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var (whereClause, parameters) = BuildWhereClause(predicate, isMySql);
            var sql = $"DELETE FROM {escapedTableName} WHERE {whereClause}";
            return await connection.ExecuteAsync(sql, parameters);
        }

        public static async Task<int> DeleteByIdAsync<T>(this IDbConnection connection, object id)
        {
            var tableName = GetTableName<T>();
            var idColumn = GetIdColumnName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var escapedIdColumn = EscapeColumnName(idColumn, isMySql);
            var sql = $"DELETE FROM {escapedTableName} WHERE {escapedIdColumn} = @Id";
            return await connection.ExecuteAsync(sql, new { Id = id });
        }

        #endregion

        #region Connection Factory Pattern

        public static async Task<IDbConnection> OpenAsync(this IDbConnectionFactory factory)
        {
            var connection = factory.CreateDbConnection();
            if (connection.State != ConnectionState.Open)
                if (connection is MySql.Data.MySqlClient.MySqlConnection mySqlConn)
                    await mySqlConn.OpenAsync();
                else
                    connection.Open();

            return connection;
        }

        #endregion

        #region DDL Operations

        public static async Task<bool> TableExistsAsync<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            return await connection.TableExistsAsync(tableName);
        }

        public static async Task<bool> TableExistsAsync(this IDbConnection connection, string tableName)
        {
            var isMySql = connection.GetType().Name.Contains("MySql");

            string sql;
            if (isMySql)
            {
                sql = @"
                    SELECT COUNT(*) 
                    FROM information_schema.tables 
                    WHERE table_schema = DATABASE() 
                    AND table_name = @TableName";
            }
            else
            {
                sql = @"
                    SELECT COUNT(*) 
                    FROM sqlite_master 
                    WHERE type = 'table' 
                    AND name = @TableName";
            }

            var count = await connection.QuerySingleAsync<int>(sql, new { TableName = tableName });
            return count > 0;
        }

        public static async Task<bool> DropTableIfExistsAsync<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);

            string sql = $"DROP TABLE IF EXISTS {escapedTableName}";
            await connection.ExecuteAsync(sql);
            return true;
        }

        public static bool DropTableIfExists<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);

            string sql = $"DROP TABLE IF EXISTS {escapedTableName}";
            connection.Execute(sql);
            return true;
        }

        #endregion

        #region Raw SQL Operations

        /// <summary>
        /// Execute raw SQL and return a list of entities
        /// </summary>
        public static async Task<List<T>> SqlListAsync<T>(this IDbConnection connection, string sql, object? parameters = null)
        {
            var result = await connection.QueryAsync<T>(sql, parameters);
            return result.ToList();
        }

        /// <summary>
        /// Execute raw SQL and return a single column of values
        /// </summary>
        public static async Task<List<T>> ColumnAsync<T>(this IDbConnection connection, string sql, object? parameters = null)
        {
            var result = await connection.QueryAsync<T>(sql, parameters);
            return result.ToList();
        }

        /// <summary>
        /// Execute raw SQL and return a single scalar value
        /// </summary>
        public static async Task<T?> ScalarAsync<T>(this IDbConnection connection, string sql, object? parameters = null)
        {
            return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
        }

        #endregion

        #region Helper Methods

        public static string GetTableName<T>()
        {
            return GetTableName(typeof(T));
        }

        public static string GetTableName(Type type)
        {
            var tableAttribute = type.GetCustomAttribute<TableAttribute>();
            return tableAttribute?.Name ?? type.Name;
        }

        private static string GetIdColumnName<T>()
        {
            var type = typeof(T);
            var keyProperty = type.GetProperties()
                .FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null)
                ?? type.GetProperty("Id");
            return keyProperty?.Name ?? "Id";
        }

        private static PropertyInfo[] GetInsertProperties<T>()
        {
            var type = typeof(T);
            return type.GetProperties()
                .Where(p => p.CanWrite &&
                           !p.GetCustomAttributes<NotMappedAttribute>().Any() &&
                           !p.GetCustomAttributes<DatabaseGeneratedAttribute>().Any() &&
                           !p.GetCustomAttributes<IgnoreAttribute>().Any() &&
                           !p.GetCustomAttributes<AutoIncrementAttribute>().Any())
                .ToArray();
        }

        private static bool IsPropertyValueCSharpDefault(PropertyInfo property, object? value)
        {
            var propertyType = property.PropertyType;
            
            // Handle nullable types
            if (Nullable.GetUnderlyingType(propertyType) != null)
            {
                return value == null;
            }
            
            // Don't treat bool false as default - too ambiguous between explicit false vs defaulted false
            if (propertyType == typeof(bool))
            {
                return false; // Never skip boolean values
            }
            
            // Handle value types (excluding bool which we handle above)
            if (propertyType.IsValueType)
            {
                var defaultValue = Activator.CreateInstance(propertyType);
                return Equals(value, defaultValue);
            }
            
            // Handle reference types (string, etc.)
            if (propertyType == typeof(string))
            {
                return value == null;
            }
            
            return value == null;
        }

        private static PropertyInfo[] GetUpdateProperties<T>()
        {
            var type = typeof(T);
            var idColumn = GetIdColumnName<T>();
            return type.GetProperties()
                .Where(p => p.CanWrite &&
                           p.Name != idColumn &&
                           !p.GetCustomAttributes<NotMappedAttribute>().Any())
                .ToArray();
        }

        public static (string whereClause, object parameters) BuildWhereClause<T>(Expression<Func<T, bool>> predicate, bool isMySql = false)
        {
            // Simple expression parser for basic conditions
            // This is a simplified version - you might want to expand this for more complex expressions
            var visitor = new WhereClauseVisitor(isMySql);
            visitor.Visit(predicate.Body);
            return (visitor.WhereClause, visitor.Parameters);
        }

        private static (string setClause, object parameters) BuildUpdateClause<T>(Expression<Func<T>> updateFields, bool isMySql = false)
        {
            // Parse the update expression to build SET clause
            var visitor = new UpdateClauseVisitor(isMySql);
            visitor.Visit(updateFields.Body);
            return (visitor.SetClause, visitor.Parameters);
        }

        private static string BuildCreateTableSql<T>(string tableName, PropertyInfo[] properties, bool isMySql)
        {
            var sb = new StringBuilder();
            var escapedTableName = EscapeTableName(tableName, isMySql);
            sb.AppendLine($"CREATE TABLE IF NOT EXISTS {escapedTableName} (");

            var columnDefinitions = new List<string>();
            var primaryKeys = new List<string>();
            var allPrimaryKeys = properties.Where(p => p.GetCustomAttribute<KeyAttribute>() != null).ToList();
            string? autoIncrementColumn = null;

            foreach (var property in properties)
            {
                var escapedColumnName = EscapeColumnName(property.Name, isMySql);
                var columnDef = new StringBuilder($"{escapedColumnName} ");

                // Check for custom field type
                var customField = property.GetCustomAttribute<CustomFieldAttribute>();
                if (customField != null)
                {
                    columnDef.Append(customField.FieldType);
                }
                else
                {
                    columnDef.Append(GetColumnType(property.PropertyType, isMySql));
                }

                // Check for primary key and auto-increment
                var isPrimaryKey = property.GetCustomAttribute<KeyAttribute>() != null;
                var isAutoIncrement = property.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;
                var propType = property.PropertyType;

                if (isPrimaryKey)
                {
                    primaryKeys.Add(escapedColumnName);
                    
                    // Handle auto-increment differently for MySQL vs SQLite
                    if ((propType == typeof(int) || propType == typeof(long)) && isAutoIncrement)
                    {
                        if (isMySql)
                        {
                            columnDef.Append(" AUTO_INCREMENT");
                        }
                        else
                        {
                            // For SQLite, we need to handle this specially
                            autoIncrementColumn = escapedColumnName;
                            columnDef.Append(" PRIMARY KEY AUTOINCREMENT");
                        }
                    }
                    else if (!isMySql && allPrimaryKeys.Count == 1)
                    {
                        // For SQLite single primary key without autoincrement
                        columnDef.Append(" PRIMARY KEY");
                    }
                }

                // Check for required/not null
                var isRequired = property.GetCustomAttribute<RequiredAttribute>() != null;
                var isNullable = Nullable.GetUnderlyingType(property.PropertyType) != null ||
                               property.PropertyType == typeof(string) ||
                               property.PropertyType.IsClass;

                if ((!isNullable || isRequired) && !(isPrimaryKey && isAutoIncrement))
                {
                    columnDef.Append(" NOT NULL");
                }

                // Check for default value
                var defaultAttr = property.GetCustomAttribute<DefaultAttribute>();
                if (defaultAttr != null)
                {
                    if (defaultAttr.Expression != null && defaultAttr.Type == typeof(DateTime))
                    {
                        if (defaultAttr.Expression == "CURRENT_TIMESTAMP")
                        {
                            columnDef.Append(isMySql ? " DEFAULT CURRENT_TIMESTAMP" : " DEFAULT CURRENT_TIMESTAMP");
                        }
                    }
                    else if (defaultAttr.Value != null)
                    {
                        var defaultValue = defaultAttr.Value;
                        if (defaultValue is string)
                        {
                            columnDef.Append($" DEFAULT '{defaultValue}'");
                        }
                        else if (defaultValue is bool boolVal)
                        {
                            columnDef.Append($" DEFAULT {(boolVal ? 1 : 0)}");
                        }
                        else
                        {
                            columnDef.Append($" DEFAULT {defaultValue}");
                        }
                    }
                }

                columnDefinitions.Add($"    {columnDef}");
            }

            sb.AppendLine(string.Join(",\n", columnDefinitions));

            // Add primary key constraint (only for MySQL or multi-column primary keys in SQLite)
            if (primaryKeys.Any())
            {
                if (isMySql)
                {
                    // MySQL always needs separate PRIMARY KEY constraint
                    sb.AppendLine($",    PRIMARY KEY ({string.Join(", ", primaryKeys)})");
                }
                else if (primaryKeys.Count > 1)
                {
                    // SQLite composite primary key
                    sb.AppendLine($",    PRIMARY KEY ({string.Join(", ", primaryKeys)})");
                }
                // For SQLite single primary key, it's already defined inline in the column definition
            }

            sb.Append(")");

            if (isMySql)
            {
                sb.Append(" ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
            }

            return sb.ToString();
        }

        private static string GetColumnType(Type propertyType, bool isMySql)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            return underlyingType.Name switch
            {
                nameof(String) => isMySql ? "VARCHAR(255)" : "TEXT",
                nameof(Int32) => isMySql ? "INT" : "INTEGER",
                nameof(Int64) => isMySql ? "BIGINT" : "INTEGER",
                nameof(Double) => isMySql ? "DOUBLE" : "REAL",
                nameof(Decimal) => isMySql ? "DECIMAL(18,2)" : "NUMERIC",
                nameof(Boolean) => isMySql ? "TINYINT(1)" : "INTEGER",
                nameof(DateTime) => isMySql ? "DATETIME" : "TEXT",
                nameof(Guid) => isMySql ? "CHAR(36)" : "TEXT",
                _ => isMySql ? "TEXT" : "TEXT"
            };
        }

        private static List<string> BuildIndexSqls<T>(string tableName, bool isMySql)
        {
            var indexSqls = new List<string>();
            var type = typeof(T);

            // Single property indexes
            foreach (var property in type.GetProperties())
            {
                var indexAttr = property.GetCustomAttribute<IndexAttribute>();
                if (indexAttr != null)
                {
                    var indexName = indexAttr.Name ?? $"IX_{tableName}_{property.Name}";
                    var unique = indexAttr.IsUnique ? "UNIQUE " : "";
                    var escapedTableName = EscapeTableName(tableName, isMySql);
                    var escapedColumnName = EscapeColumnName(property.Name, isMySql);
                    string sql;
                    if (isMySql)
                    {
                        // MySQL doesn't support IF NOT EXISTS for indexes, but we can use CREATE INDEX and ignore errors
                        sql = $"CREATE {unique}INDEX {indexName} ON {escapedTableName} ({escapedColumnName})";
                    }
                    else
                    {
                        sql = $"CREATE {unique}INDEX IF NOT EXISTS {indexName} ON {escapedTableName} ({escapedColumnName})";
                    }
                    indexSqls.Add(sql);
                }
            }

            // Composite indexes
            var compositeIndexes = type.GetCustomAttributes<CompositeIndexAttribute>();
            foreach (var compositeIndex in compositeIndexes)
            {
                var unique = compositeIndex.Unique ? "UNIQUE " : "";
                var escapedColumns = string.Join(", ", compositeIndex.FieldNames.Select(col => EscapeColumnName(col, isMySql)));
                var escapedTableName = EscapeTableName(tableName, isMySql);
                string sql;
                if (isMySql)
                {
                    // MySQL doesn't support IF NOT EXISTS for indexes
                    sql = $"CREATE {unique}INDEX {compositeIndex.Name} ON {escapedTableName} ({escapedColumns})";
                }
                else
                {
                    sql = $"CREATE {unique}INDEX IF NOT EXISTS {compositeIndex.Name} ON {escapedTableName} ({escapedColumns})";
                }
                indexSqls.Add(sql);
            }

            return indexSqls;
        }

        public static string EscapeTableName(string tableName, bool isMySql)
        {
            if (isMySql)
            {
                return $"`{tableName}`";
            }
            else
            {
                return $"[{tableName}]";
            }
        }

        private static string EscapeColumnName(string columnName, bool isMySql)
        {
            // For MySQL, always escape column names to avoid issues with case sensitivity and reserved words
            if (isMySql)
            {
                return $"`{columnName}`";
            }
            
            // For SQLite, only escape reserved keywords
            var reservedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Order", "Group", "Select", "From", "Where", "Insert", "Update", "Delete",
                "Table", "Column", "Index", "Key", "Value", "Name", "Type", "Date", "Time",
                "User", "System", "Database", "Schema", "View", "Primary", "Foreign",
                "References", "Check", "Default", "Unique", "Not", "Null", "Is", "In",
                "And", "Or", "Like", "Between", "Exists", "Having", "Count", "Sum",
                "Min", "Max", "Avg", "Distinct", "All", "Any", "Some", "Union", "Join",
                "Inner", "Left", "Right", "Full", "Outer", "On", "As", "Desc", "Asc"
            };

            if (reservedKeywords.Contains(columnName))
            {
                return $"[{columnName}]";
            }

            return columnName;
        }

        #endregion

        #region Additional ServiceStack Compatibility Methods

        /// <summary>
        /// Gets the last inserted ID (for compatibility with ServiceStack)
        /// </summary>
        public static long LastInsertId(this IDbConnection connection)
        {
            var isMySql = connection.GetType().Name.Contains("MySql");
            var sql = isMySql ? "SELECT LAST_INSERT_ID()" : "SELECT last_insert_rowid()";
            return connection.QuerySingle<long>(sql);
        }

        /// <summary>
        /// Creates a table if it doesn't exist
        /// </summary>
        public static bool CreateTableIfNotExists<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            
            // Check if table exists
            string checkTableSql;
            if (isMySql)
            {
                checkTableSql = $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{tableName}'";
            }
            else
            {
                checkTableSql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'";
            }
            
            var exists = connection.QuerySingle<int>(checkTableSql) > 0;
            if (exists)
                return false;
            
            // Create table
            var sql = BuildCreateTableSql<T>(isMySql);
            connection.Execute(sql);
            return true;
        }

        /// <summary>
        /// Creates a table if it doesn't exist (async version)
        /// </summary>
        public static async Task<bool> CreateTableIfNotExistsAsync<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var isMySql = connection.GetType().Name.Contains("MySql");
            var escapedTableName = EscapeTableName(tableName, isMySql);
            
            // Check if table exists
            string checkTableSql;
            if (isMySql)
            {
                checkTableSql = $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{tableName}'";
            }
            else
            {
                checkTableSql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'";
            }
            
            var exists = await connection.QuerySingleAsync<int>(checkTableSql) > 0;
            if (exists)
                return false;
            
            // Create table
            var sql = BuildCreateTableSql<T>(isMySql);
            await connection.ExecuteAsync(sql);
            return true;
        }

        private static string BuildCreateTableSql<T>(bool isMySql)
        {
            var tableName = GetTableName<T>();
            var escapedTableName = EscapeTableName(tableName, isMySql);
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && 
                       !Attribute.IsDefined(p, typeof(NotMappedAttribute)) &&
                       !Attribute.IsDefined(p, typeof(IgnoreAttribute)))
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {escapedTableName} (");

            var columns = new List<string>();
            var primaryKeys = new List<string>();
            var indexes = new List<string>();
            var foreignKeys = new List<string>();
            var autoIncrementColumn = "";
            
            // First pass - collect all primary keys to determine if we have single or composite keys
            var allPrimaryKeys = properties
                .Where(p => Attribute.IsDefined(p, typeof(KeyAttribute)) || Attribute.IsDefined(p, typeof(PrimaryKeyAttribute)))
                .Select(p => p.Name)
                .ToList();

            foreach (var prop in properties)
            {
                var columnName = prop.Name;
                var escapedColumnName = EscapeColumnName(columnName, isMySql);
                var columnDef = new StringBuilder();
                columnDef.Append($"    {escapedColumnName} ");

                // Determine column type
                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null || !prop.PropertyType.IsValueType;

                // Map CLR types to SQL types
                string sqlType;
                if (propType == typeof(int) || propType == typeof(Int32))
                    sqlType = isMySql ? "INT" : "INTEGER";
                else if (propType == typeof(long) || propType == typeof(Int64))
                    sqlType = isMySql ? "BIGINT" : "INTEGER";
                else if (propType == typeof(short) || propType == typeof(Int16))
                    sqlType = isMySql ? "SMALLINT" : "INTEGER";
                else if (propType == typeof(byte))
                    sqlType = isMySql ? "TINYINT" : "INTEGER";
                else if (propType == typeof(bool))
                    sqlType = isMySql ? "BOOLEAN" : "INTEGER";
                else if (propType == typeof(decimal))
                    sqlType = "DECIMAL(19,4)";
                else if (propType == typeof(double))
                    sqlType = "DOUBLE";
                else if (propType == typeof(float))
                    sqlType = "FLOAT";
                else if (propType == typeof(DateTime))
                    sqlType = isMySql ? "DATETIME" : "TEXT";
                else if (propType == typeof(Guid))
                    sqlType = isMySql ? "CHAR(36)" : "TEXT";
                else if (propType == typeof(string))
                {
                    var maxLengthAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
                    if (maxLengthAttr != null)
                        sqlType = $"VARCHAR({maxLengthAttr.Length})";
                    else
                        sqlType = "TEXT";
                }
                else
                    sqlType = "TEXT";

                // Check for CustomField attribute
                var customFieldAttr = prop.GetCustomAttribute<CustomFieldAttribute>();
                if (customFieldAttr != null)
                {
                    sqlType = customFieldAttr.FieldType;
                }

                columnDef.Append(sqlType);

                // Check for primary key
                bool isPrimaryKey = Attribute.IsDefined(prop, typeof(KeyAttribute)) || Attribute.IsDefined(prop, typeof(PrimaryKeyAttribute));
                bool isAutoIncrement = Attribute.IsDefined(prop, typeof(AutoIncrementAttribute)) || 
                    prop.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;
                
                if (isPrimaryKey)
                {
                    primaryKeys.Add(escapedColumnName);
                    
                    // Handle auto-increment differently for MySQL vs SQLite
                    if ((propType == typeof(int) || propType == typeof(long)) && isAutoIncrement)
                    {
                        if (isMySql)
                        {
                            columnDef.Append(" AUTO_INCREMENT");
                        }
                        else
                        {
                            // For SQLite, we need to handle this specially
                            autoIncrementColumn = escapedColumnName;
                            columnDef.Append(" PRIMARY KEY AUTOINCREMENT");
                        }
                    }
                    else if (!isMySql && allPrimaryKeys.Count == 1)
                    {
                        // For SQLite single primary key without autoincrement
                        columnDef.Append(" PRIMARY KEY");
                    }
                }

                // Check for required/nullable (but not for primary keys in SQLite)
                var requiredAttr = prop.GetCustomAttribute<RequiredAttribute>();
                if (requiredAttr != null || (!isNullable && !isPrimaryKey))
                {
                    columnDef.Append(" NOT NULL");
                }

                columns.Add(columnDef.ToString());

                // Check for foreign key
                var foreignKeyAttr = prop.GetCustomAttribute<ReferenceAttribute>();
                if (foreignKeyAttr != null)
                {
                    string foreignTableName;
                    if (foreignKeyAttr.ForeignType != null)
                    {
                        foreignTableName = GetTableName(foreignKeyAttr.ForeignType);
                    }
                    else
                    {
                        foreignTableName = foreignKeyAttr.ForeignTableName ?? prop.Name.Replace("Id", "");
                    }
                    
                    var escapedForeignTable = EscapeTableName(foreignTableName, isMySql);
                    var fkName = $"FK_{tableName}_{columnName}";
                    var fkDef = $"    CONSTRAINT {fkName} FOREIGN KEY ({escapedColumnName}) REFERENCES {escapedForeignTable}(Id)";
                    
                    if (!string.IsNullOrEmpty(foreignKeyAttr.OnDelete))
                    {
                        fkDef += $" ON DELETE {foreignKeyAttr.OnDelete}";
                    }
                    if (!string.IsNullOrEmpty(foreignKeyAttr.OnUpdate))
                    {
                        fkDef += $" ON UPDATE {foreignKeyAttr.OnUpdate}";
                    }
                    
                    foreignKeys.Add(fkDef);
                }
            }

            sb.AppendLine(string.Join(",\n", columns));

            // Add primary key constraint (only for MySQL or multi-column primary keys in SQLite)
            if (primaryKeys.Any())
            {
                if (isMySql)
                {
                    // MySQL always needs separate PRIMARY KEY constraint
                    sb.AppendLine($",    PRIMARY KEY ({string.Join(", ", primaryKeys)})");
                }
                else if (primaryKeys.Count > 1)
                {
                    // SQLite composite primary key
                    sb.AppendLine($",    PRIMARY KEY ({string.Join(", ", primaryKeys)})");
                }
                // For SQLite single primary key, it's already defined inline in the column definition
            }

            // Add foreign key constraints
            if (foreignKeys.Any())
            {
                foreach (var fk in foreignKeys)
                {
                    sb.AppendLine($",    {fk}");
                }
            }

            sb.Append(")");

            if (isMySql)
            {
                sb.Append(" ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Query builder for fluent API (simplified version)
        /// </summary>
        public static SqlExpression<T> From<T>(this IDbConnection connection)
        {
            return new SqlExpression<T>(connection);
        }

        /// <summary>
        /// Select with SqlExpression
        /// </summary>
        public static async Task<List<T>> SelectAsync<T>(this IDbConnection connection, SqlExpression<T> expression)
        {
            var sql = expression.ToSelectStatement();
            var result = await connection.QueryAsync<T>(sql, expression.Parameters);
            return result.ToList();
        }

        #endregion
    }

    /// <summary>
    /// Simple SQL expression builder for compatibility
    /// </summary>
    public class SqlExpression<T>
    {
        private readonly IDbConnection _connection;
        private readonly List<string> _whereConditions = new List<string>();
        private readonly List<string> _orderByColumns = new List<string>();
        private readonly DynamicParameters _parameters = new DynamicParameters();
        private int? _limit;
        private int? _offset;
        private int _paramCounter = 0;

        public DynamicParameters Parameters => _parameters;

        public SqlExpression(IDbConnection connection)
        {
            _connection = connection;
        }

        public SqlExpression<T> Where(Expression<Func<T, bool>> predicate)
        {
            var (whereClause, parameters) = DapperOrmLiteExtensions.BuildWhereClause(predicate, _connection.GetType().Name.Contains("MySql"));
            _whereConditions.Add(whereClause);
            _parameters.AddDynamicParams(parameters);
            return this;
        }

        public SqlExpression<T> Where(string sql, object parameters = null)
        {
            _whereConditions.Add(sql);
            if (parameters != null)
                _parameters.AddDynamicParams(parameters);
            return this;
        }

        public SqlExpression<T> OrderBy(Expression<Func<T, object>> column)
        {
            var memberExpression = GetMemberExpression(column.Body);
            if (memberExpression != null)
            {
                _orderByColumns.Add($"{memberExpression.Member.Name} ASC");
            }
            return this;
        }

        public SqlExpression<T> OrderByDescending(Expression<Func<T, object>> column)
        {
            var memberExpression = GetMemberExpression(column.Body);
            if (memberExpression != null)
            {
                _orderByColumns.Add($"{memberExpression.Member.Name} DESC");
            }
            return this;
        }

        public SqlExpression<T> Limit(int rows, int? skip = null)
        {
            _limit = rows;
            _offset = skip;
            return this;
        }

        public string ToSelectStatement()
        {
            var tableName = DapperOrmLiteExtensions.GetTableName<T>();
            var isMySql = _connection.GetType().Name.Contains("MySql");
            var escapedTableName = DapperOrmLiteExtensions.EscapeTableName(tableName, isMySql);
            
            var sb = new StringBuilder();
            sb.Append($"SELECT * FROM {escapedTableName}");

            if (_whereConditions.Any())
            {
                sb.Append(" WHERE ");
                sb.Append(string.Join(" AND ", _whereConditions));
            }

            if (_orderByColumns.Any())
            {
                sb.Append(" ORDER BY ");
                sb.Append(string.Join(", ", _orderByColumns));
            }

            if (_limit.HasValue)
            {
                sb.Append($" LIMIT {_limit.Value}");
                if (_offset.HasValue)
                {
                    sb.Append($" OFFSET {_offset.Value}");
                }
            }

            return sb.ToString();
        }

        private MemberExpression GetMemberExpression(Expression expression)
        {
            if (expression is MemberExpression memberExpression)
                return memberExpression;
            
            if (expression is UnaryExpression unaryExpression && unaryExpression.NodeType == ExpressionType.Convert)
                return GetMemberExpression(unaryExpression.Operand);
            
            return null;
        }
    }
}