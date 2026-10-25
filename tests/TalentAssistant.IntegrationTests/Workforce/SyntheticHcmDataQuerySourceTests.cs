using TalentAssistant.Application.Attendance;
using TalentAssistant.Application.Workforce;
using TalentAssistant.Infrastructure.Attendance;
using TalentAssistant.Infrastructure.Workforce;

namespace TalentAssistant.IntegrationTests.Workforce;

public sealed class SyntheticHcmDataQuerySourceTests
{
    [Fact]
    public async Task ComposesNameFilterAndSourceSideCount()
    {
        var result = await ExecuteAsync(new HcmDataQueryRequest(
            "employees",
            Filters: [new HcmDataQueryFilter("display_name", "contains", "Mauricio")],
            Aggregate: "count"));

        Assert.Equal(1L, Assert.IsType<long>(Assert.Single(result.Rows)["count"]));
    }

    [Fact]
    public async Task NameFiltersRemainAccentInsensitiveAcrossQuerySources()
    {
        var result = await ExecuteAsync(new HcmDataQueryRequest(
            "employees",
            Filters: [new HcmDataQueryFilter("display_name", "contains", "Juan Perez")],
            Aggregate: "count"));

        Assert.Equal(1L, Assert.IsType<long>(Assert.Single(result.Rows)["count"]));
    }

    [Fact]
    public async Task GroupsAndRanksEmployeesAtTheSourceBoundary()
    {
        var result = await ExecuteAsync(new HcmDataQueryRequest(
            "employees",
            Aggregate: "count",
            GroupBy: ["job_title"],
            OrderBy: "count",
            OrderDirection: "desc",
            Limit: 2));

        Assert.Equal(2, result.Rows.Count);
        Assert.True(
            Assert.IsType<long>(result.Rows[0]["count"])
            >= Assert.IsType<long>(result.Rows[1]["count"]));
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ComputesMinimumHireDateFromTheSelectedSourceField()
    {
        var result = await ExecuteAsync(new HcmDataQueryRequest(
            "employees",
            Aggregate: "min",
            AggregateField: "hire_date"));

        Assert.Equal(
            new DateOnly(2018, 9, 17),
            Assert.Single(result.Rows)["minimum"]);
    }

    [Fact]
    public async Task QueriesEveryApprovedSyntheticDataSet()
    {
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var requests = new[]
        {
            new HcmDataQueryRequest("employees", Fields: ["display_name"]),
            new HcmDataQueryRequest(
                "presence",
                Fields: ["employee_id", "state"],
                Filters: [new HcmDataQueryFilter("presence_date", "equals", today)]),
            new HcmDataQueryRequest("timeoff", Fields: ["reference", "type", "status"]),
            new HcmDataQueryRequest("tasks", Fields: ["reference", "title", "status"]),
            new HcmDataQueryRequest(
                "attendance_insights",
                Fields: ["incident_id", "punches", "rule_summary"])
        };

        foreach (var request in requests)
        {
            var result = await ExecuteAsync(request);
            Assert.NotEmpty(result.Rows);
            Assert.All(result.Rows, row => Assert.All(
                request.Fields!,
                field => Assert.True(row.ContainsKey(field), $"Missing {field} from {request.DataSet}.")));
        }
    }

    private static async Task<HcmDataQueryResult> ExecuteAsync(HcmDataQueryRequest request)
    {
        var workforce = new SyntheticWorkforceSource();
        var attendance = new SyntheticHcmAttendanceSource();
        var useCase = new QueryHcmData(
            new SyntheticHcmDataQuerySource(workforce, attendance),
            new HcmDataQueryValidator());
        return await useCase.ExecuteAsync(request);
    }
}
