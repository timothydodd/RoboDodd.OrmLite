using System.Data;

namespace RoboDodd.OrmLite
{
    /// <summary>
    /// Exposes Dapper's SqlMapper functionality for backward compatibility
    /// This allows code using ServiceStack.OrmLite's SqlMapper to work with RoboDodd.OrmLite
    /// </summary>
    public static class SqlMapper
    {
        /// <summary>
        /// Add a type handler for a specific type
        /// </summary>
        public static void AddTypeHandler<T>(TypeHandler<T> handler)
        {
            Dapper.SqlMapper.AddTypeHandler(handler);
        }

        /// <summary>
        /// Add a type handler for a specific type
        /// </summary>
        public static void AddTypeHandler(Type type, ITypeHandler handler)
        {
            Dapper.SqlMapper.AddTypeHandler(type, handler);
        }

        /// <summary>
        /// Reset type handlers
        /// </summary>
        public static void ResetTypeHandlers()
        {
            Dapper.SqlMapper.ResetTypeHandlers();
        }

        /// <summary>
        /// Purge the query cache
        /// </summary>
        public static void PurgeQueryCache()
        {
            Dapper.SqlMapper.PurgeQueryCache();
        }

        /// <summary>
        /// Settings for SqlMapper
        /// </summary>
        public static class Settings
        {
            /// <summary>
            /// Gets or sets the default command timeout for all queries
            /// </summary>
            public static int? CommandTimeout
            {
                get => Dapper.SqlMapper.Settings.CommandTimeout;
                set => Dapper.SqlMapper.Settings.CommandTimeout = value;
            }

            /// <summary>
            /// Disable the cache used internally by Dapper
            /// </summary>
            public static bool UseSingleResultOptimization
            {
                get => Dapper.SqlMapper.Settings.UseSingleResultOptimization;
                set => Dapper.SqlMapper.Settings.UseSingleResultOptimization = value;
            }

            /// <summary>
            /// Should Dapper use single result optimization?
            /// </summary>
            public static bool PadListExpansions
            {
                get => Dapper.SqlMapper.Settings.PadListExpansions;
                set => Dapper.SqlMapper.Settings.PadListExpansions = value;
            }
        }

        /// <summary>
        /// Type handler base class for custom type handlers
        /// Inherit from this class to create custom type handlers
        /// </summary>
        public abstract class TypeHandler<T> : Dapper.SqlMapper.TypeHandler<T>
        {
            public abstract override void SetValue(IDbDataParameter parameter, T? value);
            public abstract override T Parse(object value);
        }

        /// <summary>
        /// Type handler interface for non-generic type handlers
        /// Just a type alias for Dapper's ITypeHandler
        /// </summary>
        public interface ITypeHandler : Dapper.SqlMapper.ITypeHandler
        {
        }
    }
}