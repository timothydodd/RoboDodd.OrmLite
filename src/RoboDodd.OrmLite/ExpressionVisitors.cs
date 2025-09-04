using System.Linq.Expressions;
using System.Text;

namespace RoboDodd.OrmLite
{
    /// <summary>
    /// Visits expressions to build WHERE clauses for Dapper
    /// </summary>
    internal class WhereClauseVisitor : ExpressionVisitor
    {
        private readonly StringBuilder _sb = new();
        private readonly Dictionary<string, object> _parameters = new();
        private readonly bool _isMySql;
        private int _parameterIndex = 0;

        public WhereClauseVisitor(bool isMySql = false)
        {
            _isMySql = isMySql;
        }

        public string WhereClause => _sb.ToString();
        public Dictionary<string, object> Parameters => _parameters;

        protected override Expression VisitBinary(BinaryExpression node)
        {
            // For comparison operators, check if the right side is an arithmetic expression
            // that should be evaluated on the .NET side
            if (IsComparisonOperator(node.NodeType))
            {
                Visit(node.Left);
                
                switch (node.NodeType)
                {
                    case ExpressionType.Equal:
                        _sb.Append(" = ");
                        break;
                    case ExpressionType.NotEqual:
                        _sb.Append(" <> ");
                        break;
                    case ExpressionType.GreaterThan:
                        _sb.Append(" > ");
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        _sb.Append(" >= ");
                        break;
                    case ExpressionType.LessThan:
                        _sb.Append(" < ");
                        break;
                    case ExpressionType.LessThanOrEqual:
                        _sb.Append(" <= ");
                        break;
                }
                
                // If right side is arithmetic, evaluate it as a constant
                if (IsArithmeticExpression(node.Right))
                {
                    VisitConstant(node.Right);
                }
                else
                {
                    Visit(node.Right);
                }
                
                return node;
            }
            
            // For logical operators
            if (IsLogicalOperator(node.NodeType))
            {
                Visit(node.Left);
                
                switch (node.NodeType)
                {
                    case ExpressionType.AndAlso:
                        _sb.Append(" AND ");
                        break;
                    case ExpressionType.OrElse:
                        _sb.Append(" OR ");
                        break;
                }
                
                Visit(node.Right);
                return node;
            }
            
            // For other cases (shouldn't happen in WHERE clauses typically)
            throw new NotSupportedException($"Binary operator {node.NodeType} is not supported in WHERE clauses");
        }
        
        private bool IsComparisonOperator(ExpressionType nodeType)
        {
            return nodeType == ExpressionType.Equal ||
                   nodeType == ExpressionType.NotEqual ||
                   nodeType == ExpressionType.GreaterThan ||
                   nodeType == ExpressionType.GreaterThanOrEqual ||
                   nodeType == ExpressionType.LessThan ||
                   nodeType == ExpressionType.LessThanOrEqual;
        }
        
        private bool IsLogicalOperator(ExpressionType nodeType)
        {
            return nodeType == ExpressionType.AndAlso || nodeType == ExpressionType.OrElse;
        }
        
        private bool IsArithmeticExpression(Expression expression)
        {
            if (expression is BinaryExpression binary)
            {
                return binary.NodeType == ExpressionType.Add ||
                       binary.NodeType == ExpressionType.Subtract ||
                       binary.NodeType == ExpressionType.Multiply ||
                       binary.NodeType == ExpressionType.Divide ||
                       binary.NodeType == ExpressionType.Modulo;
            }
            return false;
        }
        
        private void VisitConstant(Expression expression)
        {
            try
            {
                var lambda = Expression.Lambda(expression);
                var compiled = lambda.Compile();
                var value = compiled.DynamicInvoke();
                
                var paramName = $"p{_parameterIndex++}";
                _parameters[paramName] = value;
                _sb.Append($"@{paramName}");
            }
            catch
            {
                // Fallback to regular visit if evaluation fails
                Visit(expression);
            }
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression?.NodeType == ExpressionType.Parameter)
            {
                var columnName = EscapeColumnName(node.Member.Name, _isMySql);
                _sb.Append(columnName);
            }
            else
            {
                // This is a captured variable or property access
                var value = GetValue(node);
                var paramName = $"p{_parameterIndex++}";
                _parameters[paramName] = value;
                _sb.Append($"@{paramName}");
            }
            return node;
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

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var paramName = $"p{_parameterIndex++}";
            _parameters[paramName] = node.Value;
            _sb.Append($"@{paramName}");
            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Not:
                    _sb.Append("NOT (");
                    Visit(node.Operand);
                    _sb.Append(")");
                    break;
                case ExpressionType.Convert:
                    Visit(node.Operand);
                    break;
                default:
                    throw new NotSupportedException($"Unary operator {node.NodeType} is not supported");
            }
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == "Contains")
            {
                // Handle different Contains scenarios
                if (node.Object != null && node.Arguments.Count == 1)
                {
                    // String.Contains(value) - e.g., u.Email.Contains("@example.com")
                    Visit(node.Object); // The string property (e.g., u.Email)
                    _sb.Append(" LIKE ");
                    
                    // Use database-specific concatenation
                    if (_isMySql)
                    {
                        _sb.Append("CONCAT('%', ");
                        Visit(node.Arguments[0]); // The search value
                        _sb.Append(", '%')");
                    }
                    else
                    {
                        // SQLite uses || for concatenation
                        _sb.Append("'%' || ");
                        Visit(node.Arguments[0]); // The search value
                        _sb.Append(" || '%'");
                    }
                }
                else if (node.Arguments.Count >= 2)
                {
                    // Collection.Contains(item) - e.g., list.Contains(u.Id)
                    Visit(node.Arguments[1]);
                    _sb.Append(" IN ");
                    Visit(node.Object ?? node.Arguments[0]);
                }
                else
                {
                    throw new NotSupportedException($"Contains method with {node.Arguments.Count} arguments is not supported");
                }
            }
            else if (node.Method.Name == "StartsWith")
            {
                // String.StartsWith(value) - e.g., u.Name.StartsWith("John")
                if (node.Object != null && node.Arguments.Count == 1)
                {
                    Visit(node.Object); // The string property
                    _sb.Append(" LIKE ");
                    
                    // Use database-specific concatenation
                    if (_isMySql)
                    {
                        _sb.Append("CONCAT(");
                        Visit(node.Arguments[0]); // The search value
                        _sb.Append(", '%')");
                    }
                    else
                    {
                        // SQLite uses || for concatenation
                        Visit(node.Arguments[0]); // The search value
                        _sb.Append(" || '%'");
                    }
                }
                else
                {
                    throw new NotSupportedException($"StartsWith method with {node.Arguments.Count} arguments is not supported");
                }
            }
            else if (node.Method.Name == "EndsWith")
            {
                // String.EndsWith(value) - e.g., u.Name.EndsWith("son")
                if (node.Object != null && node.Arguments.Count == 1)
                {
                    Visit(node.Object); // The string property
                    _sb.Append(" LIKE ");
                    
                    // Use database-specific concatenation
                    if (_isMySql)
                    {
                        _sb.Append("CONCAT('%', ");
                        Visit(node.Arguments[0]); // The search value
                        _sb.Append(")");
                    }
                    else
                    {
                        // SQLite uses || for concatenation
                        _sb.Append("'%' || ");
                        Visit(node.Arguments[0]); // The search value
                    }
                }
                else
                {
                    throw new NotSupportedException($"EndsWith method with {node.Arguments.Count} arguments is not supported");
                }
            }
            else
            {
                throw new NotSupportedException($"Method {node.Method.Name} is not supported");
            }
            return node;
        }

        private object? GetValue(Expression expression)
        {
            try
            {
                var lambda = Expression.Lambda(expression);
                var compiled = lambda.Compile();
                return compiled.DynamicInvoke();
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Visits expressions to build SET clauses for UPDATE statements
    /// </summary>
    internal class UpdateClauseVisitor : ExpressionVisitor
    {
        private readonly StringBuilder _sb = new();
        private readonly Dictionary<string, object> _parameters = new();
        private readonly bool _isMySql;
        private int _parameterIndex = 0;

        public UpdateClauseVisitor(bool isMySql = false)
        {
            _isMySql = isMySql;
        }

        public string SetClause => _sb.ToString();
        public Dictionary<string, object> Parameters => _parameters;

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            var setClauses = new List<string>();

            foreach (var binding in node.Bindings)
            {
                if (binding is MemberAssignment assignment)
                {
                    var memberName = assignment.Member.Name;
                    var value = GetValue(assignment.Expression);
                    var paramName = $"set{_parameterIndex++}";

                    _parameters[paramName] = value;
                    var escapedMemberName = EscapeColumnName(memberName, _isMySql);
                    setClauses.Add($"{escapedMemberName} = @{paramName}");
                }
            }

            _sb.Append(string.Join(", ", setClauses));
            return node;
        }

        private object? GetValue(Expression expression)
        {
            try
            {
                var lambda = Expression.Lambda(expression);
                var compiled = lambda.Compile();
                var value = compiled.DynamicInvoke();
                
                if (_isMySql && value is DateTime dateTime)
                {
                    return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                
                return value;
            }
            catch
            {
                return null;
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

    }
}