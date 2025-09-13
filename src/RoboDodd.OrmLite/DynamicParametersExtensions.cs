using Dapper;

namespace RoboDodd.OrmLite
{
    /// <summary>
    /// Extension methods for DynamicParameters to support LogMk compatibility
    /// </summary>
    public static class DynamicParametersExtensions
    {
        /// <summary>
        /// Adds a parameter only if the value is not null
        /// </summary>
        /// <param name="dynamicParameters">The dynamic parameters collection</param>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Parameter value</param>
        public static void AddIfNotNull(this DynamicParameters dynamicParameters, string name, object? value)
        {
            if (value != null)
            {
                dynamicParameters.Add($"@{name}", value);
            }
        }

        /// <summary>
        /// Adds a list of parameters to the dynamic parameters.
        /// Used in situations where you need many parameters within a SQL 'where in' clause.
        /// </summary>
        /// <typeparam name="T">Type of items in the list</typeparam>
        /// <param name="dynamicParameters">The dynamic parameters collection</param>
        /// <param name="items">The items to add as parameters</param>
        /// <param name="name">Base name for the parameters</param>
        /// <returns>List of parameter names that were added</returns>
        public static List<string> AddList<T>(this DynamicParameters dynamicParameters, IEnumerable<T> items, string name)
        {
            var uniqueItems = items.Distinct().ToList();
            var keys = new List<string>();

            for (var index = 0; index < uniqueItems.Count; index++)
            {
                var item = uniqueItems[index];
                var key = $"{name}{index}";

                keys.Add($"@{key}");
                dynamicParameters.Add(key, item);
            }

            return keys;
        }
    }
}