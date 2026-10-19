using Npgsql;
using TalentAssistant.Application.Workforce;

namespace TalentAssistant.IntegrationTests.HcmPostgres;

/// <summary>
/// AC-037 proofs against the local contract database, always as the application
/// role from the runner environment (no secret is ever printed):
/// * parameterization: hostile, SQL-shaped search values must return nothing
///   and leave the directory intact - the values never reach a statement as SQL;
/// * read-only grants: the application role cannot write to the contract views
///   nor read the physical tables; only the contract views are readable.
/// </summary>
public sealed class PostgresSourceSafetyTests : PostgresSourceTestBase
{
    [Theory]
    [InlineData("'; DROP TABLE hcm.employees; --")]
    [InlineData("'; DROP TABLE hcm_contract.talentassistant_employees; --")]
    [InlineData("\" OR 1=1 --")]
    public async Task SqlShapedSearchValuesReturnNothingAndLeaveTheDirectoryIntact(string value)
    {
        // Name filtering folds in C# and office filtering goes through a bound
        // parameter; neither path can turn the value into SQL.
        Assert.Empty(await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery(Name: value)));
        Assert.Empty(await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery(OfficeId: value)));
        Assert.Empty(await Workforce.GetTimeOffAsync(new EmployeeDataQuery(Name: value)));
        Assert.Empty(await Workforce.GetTasksAsync(new EmployeeDataQuery(Name: value)));

        Assert.Equal(
            9,
            (await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery())).Count);
    }

    [Fact]
    public async Task TheApplicationRoleCanReadTheContractViewsOnly()
    {
        await using var connection = new NpgsqlConnection(
            HcmPostgresTestEnvironment.ConnectionString());
        await connection.OpenAsync();

        // Writing through a contract view is denied by the grants (42501).
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO hcm_contract.talentassistant_employees (employee_id, display_name, office_ids) "
                + "VALUES (@employee_id, @display_name, @office_ids)";
            insert.Parameters.AddWithValue("employee_id", "employee-write-probe");
            insert.Parameters.AddWithValue("display_name", "Write Probe");
            insert.Parameters.AddWithValue("office_ids", Array.Empty<string>());

            var writeDenied = await Assert.ThrowsAsync<PostgresException>(
                () => insert.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, writeDenied.SqlState);
            Assert.Contains("permission denied", writeDenied.Message, StringComparison.Ordinal);
        }

        // The physical HCM tables behind the views are not readable either.
        await using (var selectPhysical = connection.CreateCommand())
        {
            selectPhysical.CommandText = "SELECT count(*) FROM hcm.employees";
            var readDenied = await Assert.ThrowsAsync<PostgresException>(
                () => selectPhysical.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, readDenied.SqlState);
        }

        // Sanity check: the contract views themselves stay readable.
        await using (var selectView = connection.CreateCommand())
        {
            selectView.CommandText = "SELECT count(*) FROM hcm_contract.talentassistant_employees";
            Assert.Equal(9L, Assert.IsType<long>(await selectView.ExecuteScalarAsync()));
        }
    }
}
