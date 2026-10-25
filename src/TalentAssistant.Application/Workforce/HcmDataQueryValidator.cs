using System.Globalization;

namespace TalentAssistant.Application.Workforce;

public sealed class HcmDataQueryValidationException(string message) : Exception(message);

/// <summary>
/// Validates every model-supplied identifier and value before a plan reaches a
/// source adapter. Only the versioned assistant-facing HCM schema is queryable.
/// </summary>
public sealed class HcmDataQueryValidator
{
    public const int DefaultLimit = 50;
    public const int MaximumLimit = 250;
    public const int MaximumSelectedFields = 12;
    public const int MaximumFilters = 8;
    public const int MaximumGroupFields = 3;
    public const int MaximumFilterValueLength = 256;

    private static readonly HashSet<string> RestrictedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "salary",
        "employee_salary",
        "compensation",
        "bank_account",
        "bank_account_number",
        "account_number",
        "birth_year",
        "birthyear"
    };

    public HcmValidatedDataQuery Validate(HcmDataQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HcmDataQuerySchema.TryGetDataSet(request.DataSet, out var dataSet))
        {
            throw InvalidQuery("The requested HCM data set is not available.");
        }

        var fields = ResolveFields(dataSet, request.Fields);
        var filters = ResolveFilters(dataSet, request.Filters);
        var aggregate = ResolveAggregate(request.Aggregate);
        var aggregateField = ResolveAggregateField(dataSet, aggregate, request.AggregateField);
        var groupBy = ResolveGroupBy(dataSet, request.GroupBy, aggregate);
        var (orderBy, orderByAggregate) = ResolveOrderBy(
            dataSet,
            request.OrderBy,
            aggregate);

        if (aggregate is not null && fields.Count > 0)
        {
            throw InvalidQuery("Aggregate queries use grouping fields instead of a row projection.");
        }

        if (aggregate is null && fields.Count == 0)
        {
            throw InvalidQuery("Select at least one field or request an aggregate.");
        }

        if (orderBy is not null)
        {
            var sortableFields = aggregate is null ? fields : groupBy;
            if (sortableFields.All(field => !string.Equals(
                field.Name,
                orderBy.Name,
                StringComparison.OrdinalIgnoreCase)))
            {
                throw InvalidQuery("The sort field must be selected or grouped by the query.");
            }
        }

        if (request.GroupBy is { Count: > MaximumGroupFields })
        {
            throw InvalidQuery("The query contains too many grouping fields.");
        }

        if (request.Filters is { Count: > MaximumFilters })
        {
            throw InvalidQuery("The query contains too many filters.");
        }

        var limit = request.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaximumLimit)
        {
            throw InvalidQuery($"The query limit must be between 1 and {MaximumLimit}.");
        }

        var direction = request.OrderDirection?.Trim().ToLowerInvariant();
        var descending = direction switch
        {
            null or "" or "asc" => false,
            "desc" => true,
            _ => throw InvalidQuery("The query sort direction is not supported.")
        };

        var language = string.Equals(request.Language, "en", StringComparison.Ordinal)
            ? "en"
            : "es";

        return new HcmValidatedDataQuery(
            dataSet,
            fields,
            filters,
            aggregate,
            aggregateField,
            groupBy,
            orderBy,
            orderByAggregate,
            descending,
            limit,
            language);
    }

    private static IReadOnlyList<HcmQueryField> ResolveFields(
        HcmQueryDataSet dataSet,
        IReadOnlyList<string>? requestedFields)
    {
        if (requestedFields is null)
        {
            return Array.Empty<HcmQueryField>();
        }

        if (requestedFields.Count > MaximumSelectedFields)
        {
            throw InvalidQuery("The query selects too many fields.");
        }

        var fields = new List<HcmQueryField>(requestedFields.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requested in requestedFields)
        {
            if (!seen.Add(requested))
            {
                continue;
            }

            fields.Add(ResolveField(dataSet, requested));
        }

        return fields;
    }

    private static IReadOnlyList<HcmValidatedQueryFilter> ResolveFilters(
        HcmQueryDataSet dataSet,
        IReadOnlyList<HcmDataQueryFilter>? requestedFilters)
    {
        if (requestedFilters is null)
        {
            return Array.Empty<HcmValidatedQueryFilter>();
        }

        if (requestedFilters.Count > MaximumFilters)
        {
            throw InvalidQuery("The query contains too many filters.");
        }

        var filters = new List<HcmValidatedQueryFilter>(requestedFilters.Count);
        foreach (var requested in requestedFilters)
        {
            var field = ResolveField(dataSet, requested.Field);
            var operation = ParseOperator(requested.Operator, field.Type);
            var value = ParseValue(requested.Value, field);
            filters.Add(new HcmValidatedQueryFilter(field, operation, value));
        }

        return filters;
    }

    private static HcmQueryAggregate? ResolveAggregate(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        return requested.Trim().ToLowerInvariant() switch
        {
            "count" => HcmQueryAggregate.Count,
            "min" => HcmQueryAggregate.Minimum,
            "max" => HcmQueryAggregate.Maximum,
            _ => throw InvalidQuery("The requested aggregate is not supported.")
        };
    }

    private static HcmQueryField? ResolveAggregateField(
        HcmQueryDataSet dataSet,
        HcmQueryAggregate? aggregate,
        string? requested)
    {
        if (aggregate is null or HcmQueryAggregate.Count)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                throw InvalidQuery("Count does not accept an aggregate field.");
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(requested))
        {
            throw InvalidQuery("The aggregate requires a field.");
        }

        var field = ResolveField(dataSet, requested);
        if (!IsSortable(field.Type))
        {
            throw InvalidQuery("The aggregate field does not support minimum or maximum.");
        }

        return field;
    }

    private static IReadOnlyList<HcmQueryField> ResolveGroupBy(
        HcmQueryDataSet dataSet,
        IReadOnlyList<string>? requestedFields,
        HcmQueryAggregate? aggregate)
    {
        if (requestedFields is null)
        {
            return Array.Empty<HcmQueryField>();
        }

        if (requestedFields.Count > MaximumGroupFields)
        {
            throw InvalidQuery("The query contains too many grouping fields.");
        }

        if (requestedFields.Count > 0 && aggregate is null)
        {
            throw InvalidQuery("Grouping requires an aggregate.");
        }

        var fields = new List<HcmQueryField>(requestedFields.Count);
        foreach (var requested in requestedFields)
        {
            var field = ResolveField(dataSet, requested);
            if (!IsGroupable(field.Type))
            {
                throw InvalidQuery("The grouping field type is not supported.");
            }

            if (fields.All(existing => !string.Equals(
                existing.Name,
                field.Name,
                StringComparison.OrdinalIgnoreCase)))
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private static (HcmQueryField? Field, bool IsAggregate) ResolveOrderBy(
        HcmQueryDataSet dataSet,
        string? requested,
        HcmQueryAggregate? aggregate)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return (null, false);
        }

        if (string.Equals(requested.Trim(), "count", StringComparison.OrdinalIgnoreCase)
            && aggregate == HcmQueryAggregate.Count)
        {
            return (null, true);
        }

        var field = ResolveField(dataSet, requested);
        if (!IsSortable(field.Type))
        {
            throw InvalidQuery("The sort field type is not supported.");
        }

        return (field, false);
    }

    private static HcmQueryField ResolveField(HcmQueryDataSet dataSet, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            throw InvalidQuery("A query field is required.");
        }

        var name = requested.Trim();
        if (RestrictedFields.Contains(name))
        {
            throw InvalidQuery("The requested field is restricted or unavailable.");
        }

        if (!dataSet.Fields.TryGetValue(name, out var field))
        {
            throw InvalidQuery("The requested field is restricted or unavailable.");
        }

        return field;
    }

    private static HcmQueryOperator ParseOperator(string? requested, HcmQueryFieldType type)
    {
        var operation = requested?.Trim().ToLowerInvariant() switch
        {
            "equals" => HcmQueryOperator.Equals,
            "contains" => HcmQueryOperator.Contains,
            "greater_than" => HcmQueryOperator.GreaterThan,
            "greater_than_or_equal" => HcmQueryOperator.GreaterThanOrEqual,
            "less_than" => HcmQueryOperator.LessThan,
            "less_than_or_equal" => HcmQueryOperator.LessThanOrEqual,
            _ => throw InvalidQuery("The query filter operator is not supported.")
        };

        var compatible = operation switch
        {
            HcmQueryOperator.Equals => type is not HcmQueryFieldType.Json
                and not HcmQueryFieldType.TextArray,
            HcmQueryOperator.Contains => type is HcmQueryFieldType.Text or HcmQueryFieldType.TextArray,
            _ => type is HcmQueryFieldType.Integer
                or HcmQueryFieldType.Decimal
                or HcmQueryFieldType.Date
                or HcmQueryFieldType.Time
                or HcmQueryFieldType.DateTimeOffset
        };
        if (!compatible)
        {
            throw InvalidQuery("The filter operator is not supported for this field type.");
        }

        return operation;
    }

    private static object ParseValue(string? value, HcmQueryField field)
    {
        if (value is null)
        {
            throw InvalidQuery("A query filter value is required.");
        }

        if (value.Length > MaximumFilterValueLength)
        {
            throw InvalidQuery("The query filter value exceeds the supported length.");
        }

        try
        {
            return field.Type switch
            {
                HcmQueryFieldType.Text when field.AccentInsensitive =>
                    HcmQueryText.NormalizeForNameMatching(value).ToLowerInvariant(),
                HcmQueryFieldType.Text or HcmQueryFieldType.TextArray => value,
                HcmQueryFieldType.Integer => int.Parse(value, CultureInfo.InvariantCulture),
                HcmQueryFieldType.Decimal => decimal.Parse(value, CultureInfo.InvariantCulture),
                HcmQueryFieldType.Boolean => bool.Parse(value),
                HcmQueryFieldType.Date => DateOnly.ParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),
                HcmQueryFieldType.Time => TimeOnly.Parse(value, CultureInfo.InvariantCulture),
                HcmQueryFieldType.DateTimeOffset => DateTimeOffset.Parse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                _ => throw InvalidQuery("The field type does not support filtering.")
            };
        }
        catch (FormatException)
        {
            throw InvalidQuery("The query filter value does not match the field type.");
        }
        catch (OverflowException)
        {
            throw InvalidQuery("The query filter value is outside the supported range.");
        }
    }

    private static bool IsSortable(HcmQueryFieldType type) =>
        type is HcmQueryFieldType.Text
            or HcmQueryFieldType.Integer
            or HcmQueryFieldType.Decimal
            or HcmQueryFieldType.Date
            or HcmQueryFieldType.Time
            or HcmQueryFieldType.DateTimeOffset;

    private static bool IsGroupable(HcmQueryFieldType type) =>
        type is HcmQueryFieldType.Text
            or HcmQueryFieldType.Integer
            or HcmQueryFieldType.Boolean
            or HcmQueryFieldType.Date;

    private static HcmDataQueryValidationException InvalidQuery(string message) =>
        new(message);
}
