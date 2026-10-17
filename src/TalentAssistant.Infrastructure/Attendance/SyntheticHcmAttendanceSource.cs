using TalentAssistant.Application.Attendance;

namespace TalentAssistant.Infrastructure.Attendance;

public sealed class SyntheticHcmAttendanceSource : IHcmAttendanceSource
{
    public const string Spanish = "es";
    public const string English = "en";

    internal sealed record LocalizedText(string Es, string En);

    // Single source of truth for every presentation text of the synthetic
    // dataset. Identifiers, dates, times, cities and time zones are built once
    // in CreateFixtures and are byte-identical across both variants.
    private static readonly IReadOnlyDictionary<string, LocalizedText> Texts =
        new Dictionary<string, LocalizedText>(StringComparer.Ordinal)
        {
            ["employee.001"] = new("Empleado sintético 001", "Synthetic employee 001"),
            ["employee.002"] = new("Empleado sintético 002", "Synthetic employee 002"),
            ["employee.003"] = new("Empleado sintético 003", "Synthetic employee 003"),
            ["employee.004"] = new("Empleado sintético 004", "Synthetic employee 004"),
            ["employee.005"] = new("Empleado sintético 005", "Synthetic employee 005"),
            ["office.spain"] = new("España", "Spain"),
            ["office.japan"] = new("Japón", "Japan"),
            ["punch.generic"] = new(
                "Evento de fichaje sintético del HCM",
                "Synthetic HCM punch event"),
            ["rule.review"] = new(
                "El HCM sintético informa un hallazgo de asistencia para revisión.",
                "The synthetic HCM reports an attendance finding for review."),
            ["rule.no-review"] = new(
                "El HCM sintético no informa revisión adicional para este contexto de asistencia.",
                "The synthetic HCM reports no additional review for this attendance context."),
            ["evidence.schedule"] = new(
                "El HCM aportó el turno programado y sus eventos de fichaje.",
                "The HCM supplied the scheduled shift and its punch events."),
            ["evidence.marked-review"] = new(
                "El HCM marcó este contexto de asistencia para revisión.",
                "The HCM marked this attendance context for review."),
            ["review.origin"] = new(
                "Revisar el registro de asistencia de origen.",
                "Review the originating HCM record."),
            ["timeoff.related"] = new(
                "El HCM sintético relacionó este contexto de TimeOff con el registro de asistencia.",
                "The synthetic HCM related this TimeOff context to the attendance record."),
            ["timeoff.related-birthday"] = new(
                "El HCM sintético relacionó esta solicitud de cumpleaños pendiente con el registro de asistencia.",
                "The synthetic HCM related this pending birthday request to the attendance record."),
            ["timeoff.related-rejected"] = new(
                "El HCM sintético relacionó esta solicitud de TimeOff rechazada con el registro de asistencia.",
                "The synthetic HCM related this rejected TimeOff request to the attendance record."),
            ["timeoff.related-cancelled"] = new(
                "El HCM sintético relacionó esta solicitud de TimeOff cancelada con el registro de asistencia.",
                "The synthetic HCM related this cancelled TimeOff request to the attendance record."),
            ["timeoff.vacation"] = new("Vacación", "Vacation"),
            ["timeoff.birthday"] = new("Cumpleaños", "Birthday"),
            ["timeoff.calamity"] = new("Calamidad", "Calamity"),
            ["timeoff.maternity"] = new("Baja por maternidad", "Maternity leave"),
            ["timeoff.approved"] = new("Aprobado", "Approved"),
            ["timeoff.pending"] = new("Pendiente", "Pending"),
            ["timeoff.rejected"] = new("Rechazado", "Rejected"),
            ["timeoff.cancelled"] = new("Cancelado", "Cancelled"),
            ["timeoff.unit.workday"] = new("día laborable", "workday")
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<HcmAttendanceInsight>>
        FixturesByLanguage = new Dictionary<string, IReadOnlyList<HcmAttendanceInsight>>(
            StringComparer.Ordinal)
        {
            [Spanish] = CreateFixtures(Spanish),
            [English] = CreateFixtures(English)
        };

    /// <summary>
    /// The canonical fixture catalog for a supported language. An unknown
    /// language falls back to the Spanish variant (SPEC-007 §8 assumption).
    /// </summary>
    public static IReadOnlyList<HcmAttendanceInsight> FixturesFor(string language)
    {
        if (!string.Equals(language, English, StringComparison.Ordinal))
        {
            return FixturesByLanguage[Spanish];
        }

        return FixturesByLanguage[English];
    }

    private static string T(string language, string key)
    {
        if (!Texts.TryGetValue(key, out var text))
        {
            throw new InvalidOperationException(
                $"The localized text '{key}' is missing from the synthetic catalog.");
        }

        return string.Equals(language, English, StringComparison.Ordinal)
            ? text.En
            : text.Es;
    }

    private static IReadOnlyList<HcmAttendanceInsight> CreateFixtures(string language)
    {
        return Array.AsReadOnly(
            new HcmAttendanceInsight[]
            {
            new HcmAttendanceInsight(
                "incident-demo-madrid-start",
                new HcmEmployeeReference(
                    "employee-demo-madrid",
                    T(language, "employee.001"),
                    ReadOnlyList("office-demo-madrid")),
                new HcmOfficeReference(
                    "office-demo-madrid",
                    T(language, "office.spain"),
                    "Madrid",
                    "Europe/Madrid"),
                new DateTimeOffset(2026, 10, 14, 22, 0, 0, TimeSpan.Zero),
                new HcmWorkSchedule(
                    new DateOnly(2026, 10, 15),
                    new TimeOnly(9, 0),
                    new TimeOnly(17, 0)),
                ReadOnlyList(
                    new HcmPunch(
                        new DateTimeOffset(2026, 10, 15, 7, 2, 0, TimeSpan.Zero),
                        T(language, "punch.generic")),
                    new HcmPunch(
                        new DateTimeOffset(2026, 10, 15, 11, 30, 0, TimeSpan.Zero),
                        T(language, "punch.generic"))),
                new HcmRuleFinding(
                    "HCM-SYN-RULE-001",
                    true,
                    T(language, "rule.review"),
                    new DateTimeOffset(2026, 10, 15, 7, 5, 0, TimeSpan.Zero),
                    ReadOnlyList(
                        T(language, "evidence.schedule"),
                        T(language, "evidence.marked-review")),
                    ReadOnlyList(T(language, "review.origin"))),
                new HcmTimeOffContext(
                    "timeoff-demo-madrid",
                    new DateOnly(2026, 10, 15),
                    new DateOnly(2026, 10, 16),
                    T(language, "timeoff.related"),
                    T(language, "timeoff.vacation"),
                    T(language, "timeoff.approved"),
                    "1",
                    T(language, "timeoff.unit.workday"))),
            new HcmAttendanceInsight(
                "incident-demo-tokyo-start",
                new HcmEmployeeReference(
                    "employee-demo-tokyo",
                    T(language, "employee.002"),
                    ReadOnlyList("office-demo-tokyo")),
                new HcmOfficeReference(
                    "office-demo-tokyo",
                    T(language, "office.japan"),
                    "Tokyo",
                    "Asia/Tokyo"),
                new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.Zero),
                new HcmWorkSchedule(
                    new DateOnly(2026, 10, 15),
                    new TimeOnly(9, 0),
                    new TimeOnly(18, 0)),
                ReadOnlyList(
                    new HcmPunch(
                        new DateTimeOffset(2026, 10, 15, 0, 10, 0, TimeSpan.Zero),
                        T(language, "punch.generic")),
                    new HcmPunch(
                        new DateTimeOffset(2026, 10, 15, 3, 0, 0, TimeSpan.Zero),
                        T(language, "punch.generic"))),
                new HcmRuleFinding(
                    "HCM-SYN-RULE-002",
                    false,
                    T(language, "rule.no-review"),
                    new DateTimeOffset(2026, 10, 15, 0, 15, 0, TimeSpan.Zero),
                    ReadOnlyList(T(language, "evidence.schedule")),
                    ReadOnlyList<string>()),
                new HcmTimeOffContext(
                    "timeoff-demo-tokyo-birthday",
                    new DateOnly(2026, 10, 15),
                    new DateOnly(2026, 10, 16),
                    T(language, "timeoff.related-birthday"),
                    T(language, "timeoff.birthday"),
                    T(language, "timeoff.pending"))),
            new HcmAttendanceInsight(
                "incident-demo-madrid-next-day",
                new HcmEmployeeReference(
                    "employee-demo-madrid-002",
                    T(language, "employee.003"),
                    ReadOnlyList("office-demo-madrid")),
                new HcmOfficeReference(
                    "office-demo-madrid",
                    T(language, "office.spain"),
                    "Madrid",
                    "Europe/Madrid"),
                new DateTimeOffset(2026, 10, 15, 22, 0, 0, TimeSpan.Zero),
                new HcmWorkSchedule(
                    new DateOnly(2026, 10, 16),
                    new TimeOnly(9, 0),
                    new TimeOnly(17, 0)),
                ReadOnlyList(
                    new HcmPunch(
                        new DateTimeOffset(2026, 10, 16, 7, 5, 0, TimeSpan.Zero),
                        T(language, "punch.generic"))),
                new HcmRuleFinding(
                    "HCM-SYN-RULE-003",
                    true,
                    T(language, "rule.review"),
                    new DateTimeOffset(2026, 10, 15, 22, 5, 0, TimeSpan.Zero),
                    ReadOnlyList(T(language, "evidence.schedule")),
                    ReadOnlyList(T(language, "review.origin"))),
                null),
            new HcmAttendanceInsight(
                "incident-demo-madrid-rejected",
                new HcmEmployeeReference(
                    "employee-demo-madrid-004",
                    T(language, "employee.004"),
                    ReadOnlyList("office-demo-madrid")),
                new HcmOfficeReference(
                    "office-demo-madrid",
                    T(language, "office.spain"),
                    "Madrid",
                    "Europe/Madrid"),
                new DateTimeOffset(2026, 10, 15, 22, 10, 0, TimeSpan.Zero),
                new HcmWorkSchedule(
                    new DateOnly(2026, 10, 16),
                    new TimeOnly(9, 0),
                    new TimeOnly(17, 0)),
                ReadOnlyList(
                    new HcmPunch(
                        new DateTimeOffset(2026, 10, 16, 7, 5, 0, TimeSpan.Zero),
                        T(language, "punch.generic"))),
                new HcmRuleFinding(
                    "HCM-SYN-RULE-004",
                    true,
                    T(language, "rule.review"),
                    new DateTimeOffset(2026, 10, 16, 7, 10, 0, TimeSpan.Zero),
                    ReadOnlyList(
                        T(language, "evidence.schedule"),
                        T(language, "evidence.marked-review")),
                    ReadOnlyList(T(language, "review.origin"))),
                new HcmTimeOffContext(
                    "timeoff-demo-madrid-rejected",
                    new DateOnly(2026, 10, 16),
                    new DateOnly(2026, 10, 17),
                    T(language, "timeoff.related-rejected"),
                    T(language, "timeoff.calamity"),
                    T(language, "timeoff.rejected"))),
            new HcmAttendanceInsight(
                "incident-demo-tokyo-cancelled",
                new HcmEmployeeReference(
                    "employee-demo-tokyo-002",
                    T(language, "employee.005"),
                    ReadOnlyList("office-demo-tokyo")),
                new HcmOfficeReference(
                    "office-demo-tokyo",
                    T(language, "office.japan"),
                    "Tokyo",
                    "Asia/Tokyo"),
                new DateTimeOffset(2026, 10, 16, 15, 10, 0, TimeSpan.Zero),
                new HcmWorkSchedule(
                    new DateOnly(2026, 10, 17),
                    new TimeOnly(9, 0),
                    new TimeOnly(18, 0)),
                ReadOnlyList(
                    new HcmPunch(
                        new DateTimeOffset(2026, 10, 17, 0, 10, 0, TimeSpan.Zero),
                        T(language, "punch.generic"))),
                new HcmRuleFinding(
                    "HCM-SYN-RULE-005",
                    true,
                    T(language, "rule.review"),
                    new DateTimeOffset(2026, 10, 17, 0, 15, 0, TimeSpan.Zero),
                    ReadOnlyList(
                        T(language, "evidence.schedule"),
                        T(language, "evidence.marked-review")),
                    ReadOnlyList(T(language, "review.origin"))),
                new HcmTimeOffContext(
                    "timeoff-demo-tokyo-cancelled",
                    new DateOnly(2026, 10, 17),
                    new DateOnly(2026, 10, 18),
                    T(language, "timeoff.related-cancelled"),
                    T(language, "timeoff.maternity"),
                    T(language, "timeoff.cancelled")))
            });
    }

    public Task<IReadOnlyList<HcmAttendanceInsight>> GetAttendanceInsightsAsync(
        AttendanceInsightQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = Filter(FixturesFor(query.Language), query);

        return Task.FromResult<IReadOnlyList<HcmAttendanceInsight>>(matches);
    }

    internal static IReadOnlyList<HcmAttendanceInsight> Filter(
        IReadOnlyList<HcmAttendanceInsight> insights,
        AttendanceInsightQuery query)
    {
        ArgumentNullException.ThrowIfNull(insights);
        ArgumentNullException.ThrowIfNull(query);

        if (query.FromInclusive >= query.ToExclusive)
        {
            throw new ArgumentException(
                "The start date must be earlier than the exclusive end date.",
                nameof(query));
        }

        return insights
            .Where(insight => string.IsNullOrWhiteSpace(query.EmployeeId)
                || string.Equals(
                    insight.Employee.Id,
                    query.EmployeeId,
                    StringComparison.OrdinalIgnoreCase))
            .Where(insight => IsInOfficeDateRange(insight, query))
            .ToArray();
    }

    internal static bool IsInOfficeDateRange(
        HcmAttendanceInsight insight,
        AttendanceInsightQuery query)
    {
        var officeTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            insight.Office.IanaTimeZoneId);
        var officeLocalDateTime = TimeZoneInfo.ConvertTime(
            insight.IncidentAtUtc,
            officeTimeZone);
        var officeLocalDate = DateOnly.FromDateTime(officeLocalDateTime.DateTime);

        return officeLocalDate >= query.FromInclusive
            && officeLocalDate < query.ToExclusive;
    }

    private static IReadOnlyList<T> ReadOnlyList<T>(params T[] values) =>
        Array.AsReadOnly(values);
}
