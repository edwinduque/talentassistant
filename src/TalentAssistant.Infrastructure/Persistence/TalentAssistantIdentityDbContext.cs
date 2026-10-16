using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TalentAssistant.Infrastructure.Persistence;

public sealed class TalentAssistantIdentityDbContext(
    DbContextOptions<TalentAssistantIdentityDbContext> options) : IdentityDbContext(options);
