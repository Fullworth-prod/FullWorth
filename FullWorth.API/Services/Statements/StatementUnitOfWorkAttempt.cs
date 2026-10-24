using System.Collections;
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
        var detectChanges = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            // Restore navigation references before detaching failed entities:
            // DetectChanges would otherwise rediscover a failed graph through
            // a caller's still-tracked upload or statement collection.
            foreach (var snapshot in _baseline) snapshot.RestoreNavigations();
            foreach (var entry in _db.ChangeTracker.Entries().ToArray())
                if (!_known.Contains(entry.Entity)) entry.State = EntityState.Detached;
            foreach (var snapshot in _baseline) snapshot.Restore();
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = detectChanges;
        }
    }

    private sealed class EntrySnapshot(EntityEntry entry)
    {
        private readonly NavigationSnapshot[] _navigations = entry.Navigations
            .Select(navigation => new NavigationSnapshot(navigation)).ToArray();
        private readonly PropertyValues _current = entry.CurrentValues.Clone();
        private readonly PropertyValues _original = entry.OriginalValues.Clone();
        private readonly EntityState _state = entry.State;
        private readonly HashSet<string> _modified = entry.Properties.Where(p => p.IsModified)
            .Select(p => p.Metadata.Name).ToHashSet(StringComparer.Ordinal);

        public void RestoreNavigations()
        {
            foreach (var navigation in _navigations) navigation.Restore();
        }

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

    private sealed class NavigationSnapshot(NavigationEntry navigation)
    {
        private readonly object? _value = navigation.CurrentValue;
        private readonly object[] _items = navigation.Metadata.IsCollection &&
            navigation.CurrentValue is IEnumerable collection ? collection.Cast<object>().ToArray() : [];

        public void Restore()
        {
            if (!navigation.Metadata.IsCollection)
            {
                navigation.CurrentValue = _value;
                return;
            }

            navigation.CurrentValue = _value;
            if (_value is null) return;
            var accessor = navigation.Metadata.GetCollectionAccessor()!;
            foreach (var current in ((IEnumerable)_value).Cast<object>().ToArray())
                if (!_items.Contains(current, ReferenceEqualityComparer.Instance))
                    accessor.Remove(navigation.EntityEntry.Entity, current);
            foreach (var original in _items)
                accessor.Add(navigation.EntityEntry.Entity, original, forMaterialization: false);
        }
    }
}
