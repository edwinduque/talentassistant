using TalentAssistant.Application.Workforce;

namespace TalentAssistant.UnitTests.Workforce;

public sealed class HcmDataQueryValidatorTests
{
    private readonly HcmDataQueryValidator _validator = new();

    [Fact]
    public void AcceptsCountOfEmployeesFilteredByName()
    {
        var validated = _validator.Validate(new HcmDataQueryRequest(
            "employees",
            Filters: [new HcmDataQueryFilter("display_name", "contains", "Mauricio")],
            Aggregate: "count",
            Language: "es"));

        Assert.Equal("employees", validated.DataSet.Name);
        Assert.Equal(HcmQueryAggregate.Count, validated.Aggregate);
        Assert.Equal("display_name", Assert.Single(validated.Filters).Field.Name);
        Assert.Equal(HcmQueryOperator.Contains, validated.Filters[0].Operator);
        Assert.Equal("mauricio", validated.Filters[0].Value);
        Assert.Equal("es", validated.Language);
    }

    [Fact]
    public void AllowsNonSensitiveBirthdayComponentsExposedByTheHcmContract()
    {
        var validated = _validator.Validate(new HcmDataQueryRequest(
            "employees",
            Fields: ["birthday_month", "birthday_day"]));

        Assert.Equal(
            ["birthday_month", "birthday_day"],
            validated.Fields.Select(field => field.Name));
    }

    [Theory]
    [InlineData("select")]
    [InlineData("filter")]
    [InlineData("group")]
    [InlineData("order")]
    public void RejectsSensitiveFieldsInEveryQueryPosition(string position)
    {
        var request = position switch
        {
            "select" => new HcmDataQueryRequest("employees", Fields: ["salary"]),
            "filter" => new HcmDataQueryRequest(
                "employees",
                Filters: [new HcmDataQueryFilter("account_number", "equals", "1234")],
                Aggregate: "count"),
            "group" => new HcmDataQueryRequest(
                "employees",
                Aggregate: "count",
                GroupBy: ["birth_year"]),
            "order" => new HcmDataQueryRequest(
                "employees",
                Aggregate: "count",
                OrderBy: "salary"),
            _ => throw new InvalidOperationException("Unexpected test position.")
        };

        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(request));
    }

    [Fact]
    public void RejectsUnknownDataSetsAndFields()
    {
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest("payroll", Aggregate: "count")));
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest("employees", Fields: ["private_email"])));
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest("employees", Fields: ["gender"])));
    }

    [Fact]
    public void RejectsUnsupportedOperatorsAndMalformedTypedValues()
    {
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest(
                "employees",
                Filters: [new HcmDataQueryFilter("display_name", "matches_sql", "x")],
                Aggregate: "count")));
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest(
                "employees",
                Filters: [new HcmDataQueryFilter("hire_date", "greater_than", "yesterday")],
                Aggregate: "count")));
    }

    [Fact]
    public void RequiresFieldsOrAnAggregateAndBoundsQueryLimits()
    {
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest("employees")));
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest("employees", Fields: ["display_name"], Limit: 100_000)));
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest(
                "employees",
                Filters:
                [
                    new HcmDataQueryFilter(
                        "display_name",
                        "contains",
                        new string('x', HcmDataQueryValidator.MaximumFilterValueLength + 1))
                ],
                Aggregate: "count")));
    }

    [Fact]
    public void RejectsAmbiguousAggregateProjectionAndUnselectedSortFields()
    {
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest(
                "employees",
                Fields: ["display_name"],
                Aggregate: "count")));
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest(
                "employees",
                Fields: ["display_name"],
                OrderBy: "hire_date")));
    }

    [Fact]
    public void RequiresArrayFieldsToUseContainsInsteadOfScalarEquality()
    {
        Assert.Throws<HcmDataQueryValidationException>(() => _validator.Validate(
            new HcmDataQueryRequest(
                "employees",
                Filters: [new HcmDataQueryFilter("office_ids", "equals", "office-demo-madrid")],
                Aggregate: "count")));
    }

    [Fact]
    public async Task RejectsSensitiveFieldBeforeCallingTheDataSource()
    {
        var source = new RecordingHcmDataQuerySource();
        var useCase = new QueryHcmData(source, _validator);

        await Assert.ThrowsAsync<HcmDataQueryValidationException>(() => useCase.ExecuteAsync(
            new HcmDataQueryRequest("employees", Fields: ["salary"])));

        Assert.False(source.WasCalled);
    }

    private sealed class RecordingHcmDataQuerySource : IHcmDataQuerySource
    {
        public bool WasCalled { get; private set; }

        public Task<HcmDataQueryResult> ExecuteAsync(
            HcmValidatedDataQuery query,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new HcmDataQueryResult([], Truncated: false));
        }
    }
}
