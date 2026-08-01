namespace Tidverk.Core;

/// <summary>A named project that worked days can be booked against.</summary>
public sealed record Project {
    public Project(Guid id, string name, bool isActive = true, bool isDefault = false) {
        if (id == Guid.Empty) {
            throw new ArgumentException("Project id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        IsActive = isActive;
        IsDefault = isDefault;
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsActive { get; }

    public bool IsDefault { get; }
}
