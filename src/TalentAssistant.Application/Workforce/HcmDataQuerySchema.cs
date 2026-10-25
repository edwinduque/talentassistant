using System.Collections.ObjectModel;

namespace TalentAssistant.Application.Workforce;

/// <summary>
/// Assistant-facing query contract for the versioned HCM views. Fields absent
/// here cannot be queried, even if a physical HCM table contains them.
/// </summary>
public static class HcmDataQuerySchema
{
    private static readonly IReadOnlyDictionary<string, HcmQueryDataSet> DataSets =
        CreateDataSets();

    public static IReadOnlyCollection<HcmQueryDataSet> All => DataSets.Values.ToArray();

    public static bool TryGetDataSet(string? name, out HcmQueryDataSet dataSet)
    {
        if (!string.IsNullOrWhiteSpace(name)
            && DataSets.TryGetValue(name.Trim(), out var found))
        {
            dataSet = found;
            return true;
        }

        dataSet = null!;
        return false;
    }

    private static IReadOnlyDictionary<string, HcmQueryDataSet> CreateDataSets()
    {
        var dataSets = new[]
        {
            DataSet(
                "employees",
                Field("employee_id", HcmQueryFieldType.Text),
                Field("display_name", HcmQueryFieldType.Text, accentInsensitive: true),
                Field("office_ids", HcmQueryFieldType.TextArray),
                Field("birthday_month", HcmQueryFieldType.Integer),
                Field("birthday_day", HcmQueryFieldType.Integer),
                Field("job_title", HcmQueryFieldType.Text),
                Field("last_name", HcmQueryFieldType.Text, accentInsensitive: true),
                Field("hire_date", HcmQueryFieldType.Date),
                Field("age_on_next_birthday", HcmQueryFieldType.Integer)),
            DataSet(
                "presence",
                Field("presence_date", HcmQueryFieldType.Date),
                Field("employee_id", HcmQueryFieldType.Text),
                Field("employee_display_name", HcmQueryFieldType.Text, accentInsensitive: true),
                Field("employee_office_ids", HcmQueryFieldType.TextArray),
                Field("state", HcmQueryFieldType.Text)),
            DataSet(
                "timeoff",
                Field("reference", HcmQueryFieldType.Text),
                Field("employee_id", HcmQueryFieldType.Text),
                Field("type", HcmQueryFieldType.Text),
                Field("status", HcmQueryFieldType.Text),
                Field("starts_on", HcmQueryFieldType.Date),
                Field("ends_before", HcmQueryFieldType.Date),
                Field("duration", HcmQueryFieldType.Text),
                Field("duration_unit", HcmQueryFieldType.Text),
                Field("summary", HcmQueryFieldType.Text)),
            DataSet(
                "tasks",
                Field("reference", HcmQueryFieldType.Text),
                Field("employee_id", HcmQueryFieldType.Text),
                Field("title", HcmQueryFieldType.Text),
                Field("status", HcmQueryFieldType.Text),
                Field("due_on", HcmQueryFieldType.Date),
                Field("summary", HcmQueryFieldType.Text)),
            DataSet(
                "attendance_insights",
                Field("incident_id", HcmQueryFieldType.Text),
                Field("employee_id", HcmQueryFieldType.Text),
                Field("employee_display_name", HcmQueryFieldType.Text, accentInsensitive: true),
                Field("employee_office_ids", HcmQueryFieldType.TextArray),
                Field("office_id", HcmQueryFieldType.Text),
                Field("office_country", HcmQueryFieldType.Text),
                Field("office_city", HcmQueryFieldType.Text),
                Field("office_iana_time_zone", HcmQueryFieldType.Text),
                Field("incident_at_utc", HcmQueryFieldType.DateTimeOffset),
                Field("work_date", HcmQueryFieldType.Date),
                Field("schedule_starts_at", HcmQueryFieldType.Time),
                Field("schedule_ends_at", HcmQueryFieldType.Time),
                Field("punches", HcmQueryFieldType.Json),
                Field("rule_reference", HcmQueryFieldType.Text),
                Field("rule_requires_review", HcmQueryFieldType.Boolean),
                Field("rule_summary", HcmQueryFieldType.Text),
                Field("rule_evaluated_at_utc", HcmQueryFieldType.DateTimeOffset),
                Field("rule_evidence", HcmQueryFieldType.Json),
                Field("rule_review_items", HcmQueryFieldType.Json),
                Field("related_timeoff_reference", HcmQueryFieldType.Text),
                Field("related_starts_on", HcmQueryFieldType.Date),
                Field("related_ends_before", HcmQueryFieldType.Date),
                Field("related_summary", HcmQueryFieldType.Text),
                Field("related_type", HcmQueryFieldType.Text),
                Field("related_status", HcmQueryFieldType.Text),
                Field("related_duration", HcmQueryFieldType.Text),
                Field("related_duration_unit", HcmQueryFieldType.Text))
        };

        return new ReadOnlyDictionary<string, HcmQueryDataSet>(
            dataSets.ToDictionary(dataSet => dataSet.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static HcmQueryDataSet DataSet(string name, params HcmQueryField[] fields) =>
        new(
            name,
            new ReadOnlyDictionary<string, HcmQueryField>(
                fields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase)));

    private static HcmQueryField Field(
        string name,
        HcmQueryFieldType type,
        bool accentInsensitive = false) =>
        new(name, type, accentInsensitive);
}
