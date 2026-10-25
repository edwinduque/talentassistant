using System.Security.Cryptography;
using System.Text;
using TalentAssistant.Application.Workforce;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

public sealed class PostgresHcmDataQuerySourceTests : PostgresSourceTestBase
{
    private static readonly HcmDataQueryValidator Validator = new();

    [Fact]
    public async Task SourceAggregateMatchesTheContractViewCount()
    {
        var expected = (await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery())).Count;
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Aggregate: "count"));

        var result = await DataQuery.ExecuteAsync(query);

        Assert.False(result.Truncated);
        Assert.Equal((long)expected, Assert.IsType<long>(Assert.Single(result.Rows)["count"]));
    }

    [Fact]
    public async Task NameFilterAggregateMatchesTheExistingSourceSearch()
    {
        var expected = (await Workforce.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "Mauricio"))).Count;
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Filters: [new HcmDataQueryFilter("display_name", "contains", "Mauricio")],
            Aggregate: "count"));

        var result = await DataQuery.ExecuteAsync(query);

        Assert.Equal((long)expected, Assert.IsType<long>(Assert.Single(result.Rows)["count"]));
    }

    [Fact]
    public async Task GroupedCountByAreaMatchesTheDirectory()
    {
        var employees = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery());
        var expected = employees
            .GroupBy(employee => employee.OfficeIds[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.Ordinal);

        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Aggregate: "count",
            GroupBy: ["area"]));

        var result = await DataQuery.ExecuteAsync(query);

        Assert.False(result.Truncated);
        Assert.Equal(expected.Count, result.Rows.Count);
        foreach (var row in result.Rows)
        {
            var area = Assert.IsType<string>(row["area"]);
            Assert.Equal(expected[area], Assert.IsType<long>(row["count"]));
        }
    }

    [Fact]
    public async Task GroupedCountByGenderMatchesTheDirectory()
    {
        var employees = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery());
        var expected = employees
            .GroupBy(employee => employee.Gender!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.Ordinal);

        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Aggregate: "count",
            GroupBy: ["gender"]));

        var result = await DataQuery.ExecuteAsync(query);

        Assert.False(result.Truncated);
        Assert.Equal(expected.Count, result.Rows.Count);
        foreach (var row in result.Rows)
        {
            var gender = Assert.IsType<string>(row["gender"]);
            Assert.Equal(expected[gender], Assert.IsType<long>(row["count"]));
        }
    }

    [Fact]
    public async Task ManagerAndTenureFieldsMatchTheDirectory()
    {
        var pablo = Assert.Single(await Workforce.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "Pablo Molina")));
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Fields: ["display_name", "email", "area", "gender", "manager_display_name", "years_in_company"],
            Filters: [new HcmDataQueryFilter("display_name", "contains", "Pablo Molina")]));

        var row = Assert.Single((await DataQuery.ExecuteAsync(query)).Rows);

        Assert.Equal("Pablo Molina", row["display_name"]);
        Assert.Equal(pablo.Email, row["email"]);
        Assert.Equal(pablo.OfficeIds[0], row["area"]);
        Assert.Equal(pablo.Gender, row["gender"]);
        Assert.Equal(pablo.ManagerDisplayName, row["manager_display_name"]);

        var databaseToday = await ReadDatabaseCurrentDateAsync();
        var hireDate = pablo.HireDate!.Value;
        var expectedYears = databaseToday.Year - hireDate.Year
            - (databaseToday < hireDate.AddYears(databaseToday.Year - hireDate.Year) ? 1 : 0);
        Assert.Equal(expectedYears, Assert.IsType<int>(row["years_in_company"]));
    }

    [Fact]
    public async Task NameFilterFoldsAccentsLikeTheExistingSourceSearch()
    {
        var expected = (await Workforce.SearchEmployeesAsync(
            new EmployeeSearchQuery(Name: "Juan Perez"))).Count;
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Filters: [new HcmDataQueryFilter("display_name", "contains", "Juan Perez")],
            Aggregate: "count"));

        var result = await DataQuery.ExecuteAsync(query);

        Assert.Equal((long)expected, Assert.IsType<long>(Assert.Single(result.Rows)["count"]));
    }

    [Fact]
    public async Task OfficeArrayFilterMatchesTheExistingDirectorySearch()
    {
        const string officeId = "Operaciones";
        var expected = (await Workforce.SearchEmployeesAsync(
            new EmployeeSearchQuery(OfficeId: officeId))).Count;
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Filters: [new HcmDataQueryFilter("office_ids", "contains", officeId)],
            Aggregate: "count"));

        var result = await DataQuery.ExecuteAsync(query);

        Assert.Equal((long)expected, Assert.IsType<long>(Assert.Single(result.Rows)["count"]));
    }

    [Fact]
    public async Task GroupedCountAndMinimumUseTheSameSourceFields()
    {
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Aggregate: "count",
            GroupBy: ["job_title"],
            OrderBy: "count",
            OrderDirection: "desc",
            Limit: 2));

        var grouped = await DataQuery.ExecuteAsync(query);
        Assert.Equal(2, grouped.Rows.Count);
        Assert.True(
            Assert.IsType<long>(grouped.Rows[0]["count"])
            >= Assert.IsType<long>(grouped.Rows[1]["count"]));

        var employees = await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery());
        var expectedMinimum = employees.Min(employee => employee.HireDate);
        var minimum = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Aggregate: "min",
            AggregateField: "hire_date"));
        var result = await DataQuery.ExecuteAsync(minimum);

        Assert.Equal(expectedMinimum, Assert.Single(result.Rows)["minimum"]);
    }

    [Fact]
    public async Task SQLShapedFilterValueIsBoundAndDoesNotChangeTheDirectory()
    {
        const string hostileValue = "'; DROP TABLE hcm_contract.talentassistant_employees; --";
        var countBefore = (await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery())).Count;
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Filters: [new HcmDataQueryFilter("display_name", "contains", hostileValue)],
            Aggregate: "count"));

        var result = await DataQuery.ExecuteAsync(query);

        Assert.Equal(0L, Assert.IsType<long>(Assert.Single(result.Rows)["count"]));
        Assert.Equal(countBefore, (await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery())).Count);
    }

    [Fact]
    public async Task RejectedSensitivePlanLeavesTheContractDataHashUnchanged()
    {
        var useCase = new QueryHcmData(DataQuery, Validator);
        var before = await ContractDataHashAsync();

        await Assert.ThrowsAsync<HcmDataQueryValidationException>(() => useCase.ExecuteAsync(
            new HcmDataQueryRequest("employees", Fields: ["salary"])));

        Assert.Equal(before, await ContractDataHashAsync());
    }

    [Fact]
    public async Task BirthdayComponentsAreQueryableWithoutExposingTheRawBirthYear()
    {
        var query = Validator.Validate(new HcmDataQueryRequest(
            "employees",
            Fields: ["birthday_month", "birthday_day"],
            Filters: [new HcmDataQueryFilter("employee_id", "equals", "employee-demo-mauricio")]));

        var result = await DataQuery.ExecuteAsync(query);

        var employee = Assert.Single(result.Rows);
        Assert.Equal(11, employee["birthday_month"]);
        Assert.Equal(30, employee["birthday_day"]);
        Assert.DoesNotContain("birth_year", employee.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalizedTimeOffFieldsFollowTheRequestedLanguage()
    {
        var spanishQuery = Validator.Validate(new HcmDataQueryRequest(
            "timeoff",
            Fields: ["type", "status"],
            Filters: [new HcmDataQueryFilter("employee_id", "equals", "employee-demo-juan")],
            Language: "es"));
        var englishQuery = Validator.Validate(new HcmDataQueryRequest(
            "timeoff",
            Fields: ["type", "status"],
            Filters: [new HcmDataQueryFilter("employee_id", "equals", "employee-demo-juan")],
            Language: "en"));

        var spanish = Assert.Single((await DataQuery.ExecuteAsync(spanishQuery)).Rows);
        var english = Assert.Single((await DataQuery.ExecuteAsync(englishQuery)).Rows);

        Assert.Equal("Vacación", spanish["type"]);
        Assert.Equal("Vacation", english["type"]);
        Assert.Equal("Aprobado", spanish["status"]);
        Assert.Equal("Approved", english["status"]);
    }

    [Fact]
    public async Task EveryQueryableContractFieldHasAPostgresProjection()
    {
        foreach (var dataSet in HcmDataQuerySchema.All)
        {
            var fields = dataSet.Fields.Keys.Order(StringComparer.Ordinal).ToArray();
            foreach (var batch in fields.Chunk(HcmDataQueryValidator.MaximumSelectedFields))
            {
                var query = Validator.Validate(new HcmDataQueryRequest(
                    dataSet.Name,
                    Fields: batch,
                    Limit: 1));

                var result = await DataQuery.ExecuteAsync(query);

                Assert.True(result.Rows.Count <= 1);
            }
        }
    }

    private async Task<string> ContractDataHashAsync()
    {
        var rows = await ReadContractEmployeesAsync();
        var serialized = string.Join(
            '\n',
            rows.OrderBy(row => row.Id, StringComparer.Ordinal)
                .Select(row => $"{row.Id}|{row.DisplayName}|{row.BirthdayMonth}|{row.BirthdayDay}|{row.AgeOnNextBirthday}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }
}
