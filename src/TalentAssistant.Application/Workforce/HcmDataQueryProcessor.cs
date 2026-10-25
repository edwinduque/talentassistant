using System.Collections;

namespace TalentAssistant.Application.Workforce;

/// <summary>
/// Applies the validated query semantics to the bounded in-memory HCM source.
/// PostgreSQL sources implement the same plan in SQL.
/// </summary>
public static class HcmDataQueryProcessor
{
    public static HcmDataQueryResult Execute(
        IEnumerable<IReadOnlyDictionary<string, object?>> sourceRows,
        HcmValidatedDataQuery query)
    {
        ArgumentNullException.ThrowIfNull(sourceRows);
        ArgumentNullException.ThrowIfNull(query);

        var rows = sourceRows
            .Where(row => query.Filters.All(filter => Matches(row, filter)))
            .ToArray();

        if (query.Aggregate is not null)
        {
            return Aggregate(rows, query);
        }

        var ordered = ApplyOrdering(rows, query);
        var truncated = ordered.Count > query.Limit;
        var result = ordered
            .Take(query.Limit)
            .Select(row => Project(row, query.Fields))
            .ToArray();

        return new HcmDataQueryResult(result, truncated);
    }

    private static bool Matches(
        IReadOnlyDictionary<string, object?> row,
        HcmValidatedQueryFilter filter)
    {
        if (!row.TryGetValue(filter.Field.Name, out var actual) || actual is null)
        {
            return false;
        }

        if (filter.Operator == HcmQueryOperator.Contains)
        {
            if (actual is string text && filter.Value is string substring)
            {
                var comparableText = filter.Field.AccentInsensitive
                    ? HcmQueryText.NormalizeForNameMatching(text)
                    : text;
                return comparableText.Contains(substring, StringComparison.OrdinalIgnoreCase);
            }

            if (actual is IEnumerable<string> values && filter.Value is string expected)
            {
                return values.Contains(expected, StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }

        var comparable = filter.Field.AccentInsensitive && actual is string textValue
            ? HcmQueryText.NormalizeForNameMatching(textValue)
            : actual;
        var comparison = Compare(comparable, filter.Value);
        return filter.Operator switch
        {
            HcmQueryOperator.Equals => comparison == 0,
            HcmQueryOperator.GreaterThan => comparison > 0,
            HcmQueryOperator.GreaterThanOrEqual => comparison >= 0,
            HcmQueryOperator.LessThan => comparison < 0,
            HcmQueryOperator.LessThanOrEqual => comparison <= 0,
            _ => false
        };
    }

    private static HcmDataQueryResult Aggregate(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        HcmValidatedDataQuery query)
    {
        if (query.GroupBy.Count == 0)
        {
            return new HcmDataQueryResult(
                [new Dictionary<string, object?>
                {
                    [AggregateName(query.Aggregate!.Value)] = AggregateValue(rows, query)
                }],
                Truncated: false);
        }

        var groups = rows
            .GroupBy(
                row => query.GroupBy.Select(field => GetValue(row, field.Name)).ToArray(),
                QueryGroupKeyComparer.Instance)
            .Select(group =>
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var index = 0; index < query.GroupBy.Count; index++)
                {
                    result[query.GroupBy[index].Name] = group.Key[index];
                }

                result[AggregateName(query.Aggregate!.Value)] = AggregateValue(group, query);
                return (Values: result, Count: group.LongCount());
            })
            .ToList();

        if (query.OrderByAggregate)
        {
            groups = query.Descending
                ? groups.OrderByDescending(group => group.Count).ToList()
                : groups.OrderBy(group => group.Count).ToList();
        }
        else if (query.OrderBy is not null)
        {
            groups = OrderGroups(groups, query);
        }

        var truncated = groups.Count > query.Limit;
        return new HcmDataQueryResult(
            groups
                .Take(query.Limit)
                .Select(group => (IReadOnlyDictionary<string, object?>)group.Values)
                .ToArray(),
            truncated);
    }

    private static object? AggregateValue(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        HcmValidatedDataQuery query)
    {
        if (query.Aggregate == HcmQueryAggregate.Count)
        {
            return rows.LongCount();
        }

        var values = rows
            .Select(row => GetValue(row, query.AggregateField!.Name))
            .Where(value => value is not null)
            .ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        return query.Aggregate == HcmQueryAggregate.Minimum
            ? values.Min(ComparableObjectComparer.Instance)
            : values.Max(ComparableObjectComparer.Instance);
    }

    private static List<IReadOnlyDictionary<string, object?>> ApplyOrdering(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        HcmValidatedDataQuery query)
    {
        if (query.OrderBy is null)
        {
            return rows.ToList();
        }

        var ordered = query.Descending
            ? rows.OrderByDescending(row => GetValue(row, query.OrderBy.Name), ComparableObjectComparer.Instance)
            : rows.OrderBy(row => GetValue(row, query.OrderBy.Name), ComparableObjectComparer.Instance);
        return ordered.ToList();
    }

    private static List<(Dictionary<string, object?> Values, long Count)> OrderGroups(
        List<(Dictionary<string, object?> Values, long Count)> groups,
        HcmValidatedDataQuery query)
    {
        var fieldName = query.OrderBy!.Name;
        return query.Descending
            ? groups.OrderByDescending(
                group => group.Values.GetValueOrDefault(fieldName),
                ComparableObjectComparer.Instance).ToList()
            : groups.OrderBy(
                group => group.Values.GetValueOrDefault(fieldName),
                ComparableObjectComparer.Instance).ToList();
    }

    private static IReadOnlyDictionary<string, object?> Project(
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<HcmQueryField> fields)
    {
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            projected[field.Name] = GetValue(row, field.Name);
        }

        return projected;
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> row, string field)
    {
        if (row.TryGetValue(field, out var value))
        {
            return value;
        }

        throw new InvalidOperationException("The configured HCM source returned an incomplete query row.");
    }

    private static int Compare(object left, object right)
    {
        if (left is string leftText && right is string rightText)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(leftText, rightText);
        }

        if (left.GetType() != right.GetType() && left is IConvertible && right is IConvertible)
        {
            var leftNumber = Convert.ToDecimal(left, System.Globalization.CultureInfo.InvariantCulture);
            var rightNumber = Convert.ToDecimal(right, System.Globalization.CultureInfo.InvariantCulture);
            return leftNumber.CompareTo(rightNumber);
        }

        if (left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        throw new InvalidOperationException("The configured HCM field cannot be compared.");
    }

    private static string AggregateName(HcmQueryAggregate aggregate) => aggregate switch
    {
        HcmQueryAggregate.Count => "count",
        HcmQueryAggregate.Minimum => "minimum",
        HcmQueryAggregate.Maximum => "maximum",
        _ => throw new ArgumentOutOfRangeException(nameof(aggregate))
    };

    private sealed class QueryGroupKeyComparer : IEqualityComparer<object?[]>
    {
        public static QueryGroupKeyComparer Instance { get; } = new();

        public bool Equals(object?[]? left, object?[]? right) =>
            StructuralComparisons.StructuralEqualityComparer.Equals(left, right);

        public int GetHashCode(object?[] values) =>
            StructuralComparisons.StructuralEqualityComparer.GetHashCode(values);
    }

    private sealed class ComparableObjectComparer : IComparer<object?>
    {
        public static ComparableObjectComparer Instance { get; } = new();

        public int Compare(object? left, object? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return HcmDataQueryProcessor.Compare(left, right);
        }
    }
}
