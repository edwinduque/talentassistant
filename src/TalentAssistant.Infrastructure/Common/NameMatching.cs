using System.Globalization;
using System.Text;

namespace TalentAssistant.Infrastructure.Common;

/// <summary>
/// The single name matcher shared by the HCM workforce sources (SPEC-008 S1 as
/// amended 2026-10-18: «Juan Perez» must locate «Juan Pérez», and ñ folds to n).
/// Both the synthetic source and the Postgres adapter fold display names and
/// queries through <see cref="Matches"/>, so one name query resolves the same
/// employees no matter which source serves it (single-matcher invariant).
/// Values are decomposed to Form D and stripped of non-spacing marks; the
/// comparison is <see cref="StringComparison.OrdinalIgnoreCase"/> on both folded
/// sides. Used only by the name-matching predicates - office ids keep exact
/// matching.
/// </summary>
internal static class NameMatching
{
    internal static string NormalizeForNameMatching(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>True when the folded display name contains the folded query.</summary>
    internal static bool Matches(string displayName, string queryName) =>
        NormalizeForNameMatching(displayName)
            .Contains(
                NormalizeForNameMatching(queryName),
                StringComparison.OrdinalIgnoreCase);
}
