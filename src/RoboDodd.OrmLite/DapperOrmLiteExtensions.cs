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

        public static async Task CreateTableIfNotExistsAsync<T>(this IDbConnection connection)
        {
            var tableName = GetTableName<T>();
            var type = typeof(T);
            var properties = type.GetProperties().Where(p => !p.GetCustomAttributes<NotMappedAttribute>().Any()).ToArray();

            var isMySql = connection.GetType().Name.Contains("MySql");

            var sql = BuildCreateTableSql<T>(tableName, properties, isMySql);
            await connection.ExecuteAsync(sql);

            // Create indexes
            var indexSqls = BuildIndexSqls<T>(tableName, isMySql);
            foreach (var indexSql in indexSqls)
            {
                try
                {
                    await connection.ExecuteAsync(indexSql);
                }
                catch (Exception ex) when (isMySql && (ex.Message.Contains("Duplicate key name") || ex.Message.Contains("already exists")))
                {
                    // MySQL: Index already exists, ignore this error
                    continue;
                }
            }
        }

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

        private static string GetTableName<T>()
        {
            var type = typeof(T);
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
                           !p.GetCustomAttributes<DatabaseGeneratedAttribute>().Any())
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

        private static (string whereClause, object parameters) BuildWhereClause<T>(Expression<Func<T, bool>> predicate, bool isMySql = false)
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

            foreach (var property in properties)
            {
                var columnDef = BuildColumnDefinition(property, isMySql);
                columnDefinitions.Add($"    {columnDef}");
            }

            sb.AppendLine(string.Join(",\n", columnDefinitions));
            sb.Append(")");

            if (isMySql)
            {
                sb.Append(" ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
            }

            return sb.ToString();
        }

        private static string BuildColumnDefinition(PropertyInfo property, bool isMySql)
        {
            var columnName = EscapeColumnName(property.Name, isMySql);
            var sb = new StringBuilder($"{columnName} ");

            // Check for custom field type
            var customField = property.GetCustomAttribute<CustomFieldAttribute>();
            if (customField != null)
            {
                sb.Append(customField.FieldType);
            }
            else
            {
                sb.Append(GetColumnType(property.PropertyType, isMySql));
            }

            // Check for primary key
            var isKey = property.GetCustomAttribute<KeyAttribute>() != null;
            var isAutoIncrement = property.GetCustomAttribute<DatabaseGeneratedAttribute>()?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity;

            if (isKey)
            {
                sb.Append(" PRIMARY KEY");
                if (isAutoIncrement)
                {
                    sb.Append(isMySql ? " AUTO_INCREMENT" : " AUTOINCREMENT");
                }
            }

            // Check for required/not null
            var isRequired = property.GetCustomAttribute<RequiredAttribute>() != null;
            var isNullable = Nullable.GetUnderlyingType(property.PropertyType) != null ||
                           property.PropertyType == typeof(string) ||
                           property.PropertyType.IsClass;

            if (!isNullable || isRequired)
            {
                sb.Append(" NOT NULL");
            }

            // Check for default value
            var defaultAttr = property.GetCustomAttribute<DefaultAttribute>();
            if (defaultAttr != null)
            {
                if (defaultAttr.Expression != null && defaultAttr.Type == typeof(DateTime))
                {
                    if (defaultAttr.Expression == "CURRENT_TIMESTAMP")
                    {
                        sb.Append(isMySql ? " DEFAULT CURRENT_TIMESTAMP" : " DEFAULT CURRENT_TIMESTAMP");
                    }
                }
                else if (defaultAttr.Value != null)
                {
                    var defaultValue = defaultAttr.Value;
                    if (defaultValue is string)
                    {
                        sb.Append($" DEFAULT '{defaultValue}'");
                    }
                    else if (defaultValue is bool boolVal)
                    {
                        sb.Append($" DEFAULT {(boolVal ? 1 : 0)}");
                    }
                    else
                    {
                        sb.Append($" DEFAULT {defaultValue}");
                    }
                }
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
                var unique = compositeIndex.IsUnique ? "UNIQUE " : "";
                var escapedColumns = string.Join(", ", compositeIndex.Properties.Select(col => EscapeColumnName(col, isMySql)));
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

        private static string EscapeTableName(string tableName, bool isMySql)
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
    }
}