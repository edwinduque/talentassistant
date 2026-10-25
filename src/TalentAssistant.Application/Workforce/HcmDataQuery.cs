namespace TalentAssistant.Application.Workforce;

public enum HcmQueryFieldType
{
    Text,
    TextArray,
    Integer,
    Decimal,
    Boolean,
    Date,
    Time,
    DateTimeOffset,
    Json
}

public enum HcmQueryOperator
{
    Equals,
    Contains,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public enum HcmQueryAggregate
{
    Count,
    Minimum,
    Maximum
}

public sealed record HcmQueryField(
    string Name,
    HcmQueryFieldType Type,
    bool AccentInsensitive = false);

public sealed record HcmQueryDataSet(
    string Name,
    IReadOnlyDictionary<string, HcmQueryField> Fields);

/// <summary>
/// Model-facing query intent. Values are always strings at the tool boundary
/// and are parsed to the declared field type before reaching a data source.
/// </summary>
public sealed record HcmDataQueryRequest(
    string DataSet,
    IReadOnlyList<string>? Fields = null,
    IReadOnlyList<HcmDataQueryFilter>? Filters = null,
    string? Aggregate = null,
    string? AggregateField = null,
    IReadOnlyList<string>? GroupBy = null,
    string? OrderBy = null,
    string? OrderDirection = null,
    int? Limit = null,
    string Language = "es");

public sealed record HcmDataQueryFilter(
    string Field,
    string Operator,
    string Value);

public sealed record HcmValidatedQueryFilter(
    HcmQueryField Field,
    HcmQueryOperator Operator,
    object Value);

/// <summary>
/// Validated query plan passed from Application to the configured HCM source.
/// It contains logical identifiers only; source adapters resolve them to
/// trusted physical identifiers and bind every value as a parameter.
/// </summary>
public sealed class HcmValidatedDataQuery
{
    internal HcmValidatedDataQuery(
        HcmQueryDataSet dataSet,
        IReadOnlyList<HcmQueryField> fields,
        IReadOnlyList<HcmValidatedQueryFilter> filters,
        HcmQueryAggregate? aggregate,
        HcmQueryField? aggregateField,
        IReadOnlyList<HcmQueryField> groupBy,
        HcmQueryField? orderBy,
        bool orderByAggregate,
        bool descending,
        int limit,
        string language)
    {
        DataSet = dataSet;
        Fields = fields;
        Filters = filters;
        Aggregate = aggregate;
        AggregateField = aggregateField;
        GroupBy = groupBy;
        OrderBy = orderBy;
        OrderByAggregate = orderByAggregate;
        Descending = descending;
        Limit = limit;
        Language = language;
    }

    public HcmQueryDataSet DataSet { get; }
    public IReadOnlyList<HcmQueryField> Fields { get; }
    public IReadOnlyList<HcmValidatedQueryFilter> Filters { get; }
    public HcmQueryAggregate? Aggregate { get; }
    public HcmQueryField? AggregateField { get; }
    public IReadOnlyList<HcmQueryField> GroupBy { get; }
    public HcmQueryField? OrderBy { get; }
    public bool OrderByAggregate { get; }
    public bool Descending { get; }
    public int Limit { get; }
    public string Language { get; }
}

public sealed record HcmDataQueryResult(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    bool Truncated);

public interface IHcmDataQuerySource
{
    Task<HcmDataQueryResult> ExecuteAsync(
        HcmValidatedDataQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class QueryHcmData(
    IHcmDataQuerySource querySource,
    HcmDataQueryValidator validator)
{
    public async Task<HcmDataQueryResult> ExecuteAsync(
        HcmDataQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var validated = validator.Validate(request);
        return await querySource.ExecuteAsync(validated, cancellationToken);
    }
}
