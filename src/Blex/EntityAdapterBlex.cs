using System.Text.Json.Serialization;

namespace Blex;

/// <summary>
/// Generates CRUD operations and an initial state for a normalized <see cref="EntityStateBlex{TEntity, TKey}"/>.
/// Holds the id selector (not serialized), so create one per entity type, typically as a static field
/// on the store. Mirrors Redux Toolkit's <c>createEntityAdapter</c>.
/// </summary>
public sealed class EntityAdapterBlex<TEntity, TKey>
    where TKey : notnull
{
    private readonly Func<TEntity, TKey> _selectId;
    private readonly IComparer<TEntity>? _sortComparer;

    /// <summary>Creates an adapter that derives an entity's id via <paramref name="selectId"/>.</summary>
    public EntityAdapterBlex(Func<TEntity, TKey> selectId)
        : this(selectId, null)
    {
    }

    /// <summary>
    /// Creates an adapter that keeps <see cref="EntityStateBlex{TEntity, TKey}.Ids"/> sorted by
    /// <paramref name="sortComparer"/> after every operation (like Redux Toolkit's
    /// <c>sortComparer</c>). Without a comparer, insertion order is preserved.
    /// </summary>
    public EntityAdapterBlex(Func<TEntity, TKey> selectId, IComparer<TEntity>? sortComparer)
    {
        ArgumentNullException.ThrowIfNull(selectId);
        _selectId = selectId;
        _sortComparer = sortComparer;
    }

    /// <summary>Builds the resulting state, re-sorting ids when a sort comparer is configured.</summary>
    private EntityStateBlex<TEntity, TKey> Build(List<TKey> ids, Dictionary<TKey, TEntity> map)
    {
        if (_sortComparer is not null)
            ids = ids.OrderBy(id => map[id], _sortComparer).ToList(); // stable sort

        return new EntityStateBlex<TEntity, TKey>(ids, map);
    }

    /// <summary>The empty initial state for this entity type.</summary>
    public EntityStateBlex<TEntity, TKey> GetInitialState() => EntityStateBlex<TEntity, TKey>.Empty;

    /// <summary>Initial state seeded with the supplied entities.</summary>
    public EntityStateBlex<TEntity, TKey> GetInitialState(IEnumerable<TEntity> entities)
        => SetAll(EntityStateBlex<TEntity, TKey>.Empty, entities);

    /// <summary>Adds an entity. No-op if its id already exists.</summary>
    public EntityStateBlex<TEntity, TKey> AddOne(EntityStateBlex<TEntity, TKey> state, TEntity entity)
    {
        var id = _selectId(entity);
        if (state.Entities.ContainsKey(id))
            return state;

        var ids = new List<TKey>(state.Ids) { id };
        var map = ToDictionary(state);
        map[id] = entity;
        return Build(ids, map);
    }

    /// <summary>Adds many entities, ignoring any whose id already exists.</summary>
    public EntityStateBlex<TEntity, TKey> AddMany(EntityStateBlex<TEntity, TKey> state, IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var ids = new List<TKey>(state.Ids);
        var map = ToDictionary(state);
        var changed = false;
        foreach (var entity in entities)
        {
            var id = _selectId(entity);
            if (map.ContainsKey(id))
                continue;
            ids.Add(id);
            map[id] = entity;
            changed = true;
        }

        // A no-op must return the *same* instance: state fields compare by reference, so handing
        // back an equivalent-but-new instance would raise a change notification and record a
        // time-travel action for a mutation that never happened.
        return changed ? Build(ids, map) : state;
    }

    /// <summary>Adds or replaces an entity (matched by id).</summary>
    public EntityStateBlex<TEntity, TKey> UpsertOne(EntityStateBlex<TEntity, TKey> state, TEntity entity)
    {
        var id = _selectId(entity);
        var map = ToDictionary(state);
        var existed = map.ContainsKey(id);
        map[id] = entity;
        var ids = existed ? new List<TKey>(state.Ids) : new List<TKey>(state.Ids) { id };
        return Build(ids, map);
    }

    /// <summary>Adds or replaces many entities (matched by id).</summary>
    public EntityStateBlex<TEntity, TKey> UpsertMany(EntityStateBlex<TEntity, TKey> state, IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var ids = new List<TKey>(state.Ids);
        var map = ToDictionary(state);
        var changed = false;
        foreach (var entity in entities)
        {
            var id = _selectId(entity);
            if (!map.ContainsKey(id))
                ids.Add(id);
            map[id] = entity;
            changed = true;
        }

        return changed ? Build(ids, map) : state;
    }

    /// <summary>Applies an update function to one entity, matched by id. No-op if absent.</summary>
    public EntityStateBlex<TEntity, TKey> UpdateOne(EntityStateBlex<TEntity, TKey> state, TKey id, Func<TEntity, TEntity> update)
    {
        if (!state.Entities.TryGetValue(id, out var existing))
            return state;

        var updated = update(existing);
        var newId = _selectId(updated);

        // An updater that returns an equal entity (a record with the same values, or the very same
        // instance) is a no-op; returning the same state keeps it from raising a notification and
        // recording a time-travel action. This is what makes Map/UpdateMany over unchanged
        // entities free.
        if (EqualityComparer<TKey>.Default.Equals(newId, id)
            && EqualityComparer<TEntity>.Default.Equals(existing, updated))
        {
            return state;
        }

        var map = ToDictionary(state);

        if (EqualityComparer<TKey>.Default.Equals(newId, id))
        {
            map[id] = updated;
            // The id list is never mutated after construction, so it can be shared when order
            // is insertion-based; a sort comparer may need to re-position the updated entity.
            return _sortComparer is null
                ? new EntityStateBlex<TEntity, TKey>(state.Ids, map)
                : Build(new List<TKey>(state.Ids), map);
        }

        // Id changed.
        map.Remove(id);
        var ids = new List<TKey>(state.Ids);
        if (map.ContainsKey(newId))
        {
            // The new id already belongs to another entity: overwrite it and drop the old
            // slot (a positional swap would duplicate the id in the ordered list).
            map[newId] = updated;
            ids.Remove(id);
        }
        else
        {
            map[newId] = updated;
            ids[ids.IndexOf(id)] = newId;
        }

        return Build(ids, map);
    }

    /// <summary>Applies an update function to each entity matched by id. Missing ids are ignored.</summary>
    public EntityStateBlex<TEntity, TKey> UpdateMany(EntityStateBlex<TEntity, TKey> state, IEnumerable<TKey> ids, Func<TEntity, TEntity> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var result = state;
        foreach (var id in ids)
            result = UpdateOne(result, id, update);
        return result;
    }

    /// <summary>Applies a transform to every entity (like Redux Toolkit's <c>map</c>).</summary>
    public EntityStateBlex<TEntity, TKey> Map(EntityStateBlex<TEntity, TKey> state, Func<TEntity, TEntity> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return UpdateMany(state, new List<TKey>(state.Ids), transform);
    }

    /// <summary>Removes the entity with the given id. No-op if absent.</summary>
    public EntityStateBlex<TEntity, TKey> RemoveOne(EntityStateBlex<TEntity, TKey> state, TKey id)
    {
        if (!state.Entities.ContainsKey(id))
            return state;

        var map = ToDictionary(state);
        map.Remove(id);
        var ids = new List<TKey>(state.Ids);
        ids.Remove(id);
        return Build(ids, map);
    }

    /// <summary>Removes the entities with the given ids. Ids that are not present are ignored.</summary>
    public EntityStateBlex<TEntity, TKey> RemoveMany(EntityStateBlex<TEntity, TKey> state, IEnumerable<TKey> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var toRemove = new HashSet<TKey>(ids);
        var map = ToDictionary(state);
        var removed = 0;
        foreach (var id in toRemove)
        {
            if (map.Remove(id))
                removed++;
        }

        if (removed == 0)
            return state;

        var remaining = new List<TKey>(state.Ids.Count - removed);
        foreach (var id in state.Ids)
        {
            if (!toRemove.Contains(id))
                remaining.Add(id);
        }

        // Removal preserves the relative order of what is left, so a sorted state stays sorted
        // and there is nothing for Build's comparer pass to do.
        return new EntityStateBlex<TEntity, TKey>(remaining, map);
    }

    /// <summary>Removes every entity.</summary>
    public EntityStateBlex<TEntity, TKey> RemoveAll(EntityStateBlex<TEntity, TKey> state)
        => state.Count == 0 ? state : EntityStateBlex<TEntity, TKey>.Empty;

    /// <summary>Replaces the entire collection with the supplied entities.</summary>
    public EntityStateBlex<TEntity, TKey> SetAll(EntityStateBlex<TEntity, TKey> state, IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var ids = new List<TKey>();
        var map = new Dictionary<TKey, TEntity>();
        foreach (var entity in entities)
        {
            var id = _selectId(entity);
            if (!map.ContainsKey(id))
                ids.Add(id);
            map[id] = entity;
        }

        // Clearing an already-empty collection changes nothing; keep the instance so it does not
        // register as a mutation.
        if (ids.Count == 0 && state.Count == 0)
            return state;

        return Build(ids, map);
    }

    private static Dictionary<TKey, TEntity> ToDictionary(EntityStateBlex<TEntity, TKey> state)
        => new(state.Entities);
}
