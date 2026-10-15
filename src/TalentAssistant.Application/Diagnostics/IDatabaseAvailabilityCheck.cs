namespace TalentAssistant.Application.Diagnostics;

public interface IDatabaseAvailabilityCheck
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}
