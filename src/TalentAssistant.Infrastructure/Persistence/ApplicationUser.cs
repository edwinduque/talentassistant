using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TalentAssistant.Infrastructure.Persistence;

/// <summary>
/// Local user with the profile attributes required by SPEC-006. The password
/// handling, hashing and lockout continue to come from ASP.NET Core Identity;
/// no business rules live here.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    public const string DefaultPreferredLanguage = "es";

    /// <summary>Optional display name shown in the layout instead of the user name.</summary>
    [MaxLength(100)]
    public string? FullName { get; set; }

    /// <summary>Preferred UI language code; only the closed catalog es/en is accepted.</summary>
    [MaxLength(5)]
    public string PreferredLanguage { get; set; } = DefaultPreferredLanguage;
}
