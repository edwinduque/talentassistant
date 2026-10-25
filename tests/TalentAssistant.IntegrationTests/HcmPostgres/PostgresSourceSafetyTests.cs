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

        var count = (await Workforce.SearchEmployeesAsync(new EmployeeSearchQuery())).Count;
        Assert.True(
            count > 9,
            $"Expected a catalog wider than the nine fixtures, got {count}.");
    }

    [Fact]
    public async Task TheApplicationRoleCanReadTheContractViewsOnly()
    {
        await using var connection = new NpgsqlConnection(
            HcmPostgresTestEnvironment.ConnectionString());
        await connection.OpenAsync();

        // Writing through a contract view is impossible either way: with an
        // auto-updatable view the grant denies it (42501, "permission denied");
        // the rewired view projects derived columns, so PostgreSQL refuses the
        // statement before any privilege check (55000, "cannot insert into
        // view"). Both states mean the write never reaches the data
        // (SPEC-011 AC-037, SPEC-012 T3 reconciliation).
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
            Assert.True(
                writeDenied.SqlState == PostgresErrorCodes.InsufficientPrivilege
                    || writeDenied.SqlState == PostgresErrorCodes.ObjectNotInPrerequisiteState,
                $"Unexpected SQLSTATE {writeDenied.SqlState}: {writeDenied.MessageText}");
            Assert.True(
                writeDenied.Message.Contains("permission denied", StringComparison.Ordinal)
                    || writeDenied.Message.Contains("cannot insert", StringComparison.Ordinal),
                $"Unexpected denial message: {writeDenied.Message}");
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
            var viewCount = Assert.IsType<long>(await selectView.ExecuteScalarAsync());
            Assert.True(
                viewCount > 9L,
                $"The contract view must serve more than the nine fixtures, got {viewCount}.");
        }
    }
}
