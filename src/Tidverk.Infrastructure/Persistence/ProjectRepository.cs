using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Tidverk.Core;

namespace Tidverk.Infrastructure.Persistence;

public sealed class ProjectRepository(IDbContextFactory<TidverkDbContext> contextFactory) : IProjectRepository {
    public async Task<Project> EnsureDefaultAsync(string name, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        string trimmedName = name.Trim();
        await using TidverkDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Clearing the old default and setting the new one must land together, otherwise a failure
        // in between leaves the user with no default project or two of them.
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Projects
            .Where(item => item.IsDefault)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDefault, false), cancellationToken);

        ProjectEntity? entity = await context.Projects.SingleOrDefaultAsync(item => item.Name == trimmedName, cancellationToken);
        if (entity is null) {
            entity = new ProjectEntity { Id = Guid.NewGuid(), Name = trimmedName, IsActive = true };
            context.Projects.Add(entity);
        }

        entity.IsDefault = true;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(entity.Id, entity.Name, entity.IsActive, true);
    }
}
