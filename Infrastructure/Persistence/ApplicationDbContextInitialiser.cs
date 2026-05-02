using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ApplicationDbContextInitialiser(ApplicationDbContext context)
{
    public Task InitialiseAsync() => context.Database.MigrateAsync();
}
