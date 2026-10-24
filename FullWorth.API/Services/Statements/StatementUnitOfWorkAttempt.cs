using FullWorth.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FullWorth.API.Services.Statements;

/// <summary>
/// Restores in-memory staging after a failed attempt. This is not a database
/// rollback and cannot resolve an unknown outcome after a remote commit.
/// </summary>
internal sealed class StatementUnitOfWorkAttempt
{
    private readonly FullWorthDbContext _db;
    private readonly EntrySnapshot[] _baseline;
    private readonly HashSet<object> _known;

    public StatementUnitOfWorkAttempt(FullWorthDbContext db, params object[] additionalEntities)
    {
        _db = db;
        var entries = db.ChangeTracker.Entries().ToArray();
        _baseline = entries.Concat(additionalEntities.Select(entity => db.Entry(entity)))
            .DistinctBy(entry => entry.Entity)
            .Select(entry => new EntrySnapshot(entry)).ToArray();
        _known = entries.Select(entry => entry.Entity).ToHashSet();
    }

    public void Restore()
    {
        foreach (var entry in _db.ChangeTracker.Entries().ToArray())
            if (!_known.Contains(entry.Entity)) entry.State = EntityState.Detached;
        foreach (var snapshot in _baseline) snapshot.Restore();
    }

    private sealed class EntrySnapshot(EntityEntry entry)
    {
        private readonly PropertyValues _current = entry.CurrentValues.Clone();
        private readonly PropertyValues _original = entry.OriginalValues.Clone();
        private readonly EntityState _state = entry.State;
        private readonly HashSet<string> _modified = entry.Properties.Where(p => p.IsModified)
            .Select(p => p.Metadata.Name).ToHashSet(StringComparer.Ordinal);

        public void Restore()
        {
            entry.CurrentValues.SetValues(_current);
            entry.OriginalValues.SetValues(_original);
            entry.State = _state;
            if (_state == EntityState.Modified)
                foreach (var property in entry.Properties)
                    property.IsModified = _modified.Contains(property.Metadata.Name);
        }
    }
}
