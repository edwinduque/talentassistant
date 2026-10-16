using Microsoft.AspNetCore.Components;

namespace TalentAssistant.Web.Authentication;

/// <summary>
/// Client-side redirect used by <c>AuthorizeRouteView</c> when the
/// visitor has no session. Safe during prerender because the project
/// disables the prerender navigation exception.
/// </summary>
public sealed class RedirectToLogin : ComponentBase
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized() =>
        Navigation.NavigateTo("/login");
}
