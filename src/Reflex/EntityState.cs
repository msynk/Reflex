using System.Text.Json.Serialization;

namespace Reflex;

/// <summary>
/// An immutable, normalized collection: an ordered list of ids plus an id-keyed entity map.
/// Designed to be stored as a single <c>[State]</c> field. Every mutation returns a new instance,
/// so change detection (and therefore notifications and time-travel) works correctly. Create and
/// mutate instances through an <see cref="EntityAdapter{TEntity, TKey}"/>.
/// </summary>
/// <typeparam name="TEntity">The stored entity type.</typeparam>
/// <typeparam name="TKey">The id type (must be non-null and JSON-serializable as a dictionary key).</typeparam>
public sealed class EntityState<TEntity, TKey>
    where TKey : notnull
{
    /// <summary>Creates a normalized state from an ordered id list and matching entity map.</summary>
    [JsonConstructor]
    public EntityState(IReadOnlyList<TKey> ids, IReadOnlyDictionary<TKey, TEntity> entities)
    {
        Ids = ids;
        Entities = entities;
    }

    /// <summary>The ids in insertion/display order.</summary>
    public IReadOnlyList<TKey> Ids { get; }

    /// <summary>The id-to-entity map.</summary>
    public IReadOnlyDictionary<TKey, TEntity> Entities { get; }

    /// <summary>An empty state.</summary>
    public static EntityState<TEntity, TKey> Empty { get; } =
        new(Array.Empty<TKey>(), new Dictionary<TKey, TEntity>());

    /// <summary>Number of stored entities.</summary>
    [JsonIgnore]
    public int Count => Ids.Count;

    /// <summary>Entities in id order.</summary>
    [JsonIgnore]
    public IEnumerable<TEntity> All
    {
        get
        {
            foreach (var id in Ids)
                yield return Entities[id];
        }
    }

    /// <summary>Whether an entity with the given id exists.</summary>
    public bool Contains(TKey id) => Entities.ContainsKey(id);

    /// <summary>Returns the entity for an id, or <c>default</c> if absent.</summary>
    public TEntity? Find(TKey id) => Entities.TryGetValue(id, out var e) ? e : default;
}

/// <summary>
/// Generates CRUD operations and an initial state for a normalized <see cref="EntityState{TEntity, TKey}"/>.
/// Holds the id selector (not serialized), so create one per entity type, typically as a static field
/// on the store. Mirrors Redux Toolkit's <c>createEntityAdapter</c>.
/// </summary>
public sealed class EntityAdapter<TEntity, TKey>
    where TKey : notnull
{
    private readonly Func<TEntity, TKey> _selectId;

    /// <summary>Creates an adapter that derives an entity's id via <paramref name="selectId"/>.</summary>
    public EntityAdapter(Func<TEntity, TKey> selectId)
    {
        ArgumentNullException.ThrowIfNull(selectId);
        _selectId = selectId;
    }

    /// <summary>The empty initial state for this entity type.</summary>
    public EntityState<TEntity, TKey> GetInitialState() => EntityState<TEntity, TKey>.Empty;

    /// <summary>Initial state seeded with the supplied entities.</summary>
    public EntityState<TEntity, TKey> GetInitialState(IEnumerable<TEntity> entities)
        => SetAll(EntityState<TEntity, TKey>.Empty, entities);

    /// <summary>Adds an entity. No-op if its id already exists.</summary>
    public EntityState<TEntity, TKey> AddOne(EntityState<TEntity, TKey> state, TEntity entity)
    {
        var id = _selectId(entity);
        if (state.Entities.ContainsKey(id))
            return state;

        var ids = new List<TKey>(state.Ids) { id };
        var map = ToDictionary(state);
        map[id] = entity;
        return new EntityState<TEntity, TKey>(ids, map);
    }

    /// <summary>Adds many entities, ignoring any whose id already exists.</summary>
    public EntityState<TEntity, TKey> AddMany(EntityState<TEntity, TKey> state, IEnumerable<TEntity> entities)
    {
        var ids = new List<TKey>(state.Ids);
        var map = ToDictionary(state);
        foreach (var entity in entities)
        {
            var id = _selectId(entity);
            if (map.ContainsKey(id))
                continue;
            ids.Add(id);
            map[id] = entity;
        }

        return new EntityState<TEntity, TKey>(ids, map);
    }

    /// <summary>Adds or replaces an entity (matched by id).</summary>
    public EntityState<TEntity, TKey> UpsertOne(EntityState<TEntity, TKey> state, TEntity entity)
    {
        var id = _selectId(entity);
        var map = ToDictionary(state);
        var existed = map.ContainsKey(id);
        map[id] = entity;
        var ids = existed ? new List<TKey>(state.Ids) : new List<TKey>(state.Ids) { id };
        return new EntityState<TEntity, TKey>(ids, map);
    }

    /// <summary>Adds or replaces many entities (matched by id).</summary>
    public EntityState<TEntity, TKey> UpsertMany(EntityState<TEntity, TKey> state, IEnumerable<TEntity> entities)
    {
        var ids = new List<TKey>(state.Ids);
        var map = ToDictionary(state);
        foreach (var entity in entities)
        {
            var id = _selectId(entity);
            if (!map.ContainsKey(id))
                ids.Add(id);
            map[id] = entity;
        }

        return new EntityState<TEntity, TKey>(ids, map);
    }

    /// <summary>Applies an update function to one entity, matched by id. No-op if absent.</summary>
    public EntityState<TEntity, TKey> UpdateOne(EntityState<TEntity, TKey> state, TKey id, Func<TEntity, TEntity> update)
    {
        if (!state.Entities.TryGetValue(id, out var existing))
            return state;

        var updated = update(existing);
        var newId = _selectId(updated);
        var map = ToDictionary(state);

        if (EqualityComparer<TKey>.Default.Equals(newId, id))
        {
            map[id] = updated;
            return new EntityState<TEntity, TKey>(new List<TKey>(state.Ids), map);
        }

        // Id changed: replace in place positionally.
        map.Remove(id);
        map[newId] = updated;
        var ids = new List<TKey>(state.Ids);
        ids[ids.IndexOf(id)] = newId;
        return new EntityState<TEntity, TKey>(ids, map);
    }

    /// <summary>Removes the entity with the given id. No-op if absent.</summary>
    public EntityState<TEntity, TKey> RemoveOne(EntityState<TEntity, TKey> state, TKey id)
    {
        if (!state.Entities.ContainsKey(id))
            return state;

        var map = ToDictionary(state);
        map.Remove(id);
        var ids = new List<TKey>(state.Ids);
        ids.Remove(id);
        return new EntityState<TEntity, TKey>(ids, map);
    }

    /// <summary>Removes the entities with the given ids.</summary>
    public EntityState<TEntity, TKey> RemoveMany(EntityState<TEntity, TKey> state, IEnumerable<TKey> ids)
    {
        var toRemove = new HashSet<TKey>(ids);
        var map = ToDictionary(state);
        foreach (var id in toRemove)
            map.Remove(id);
        var remaining = new List<TKey>();
        foreach (var id in state.Ids)
        {
            if (!toRemove.Contains(id))
                remaining.Add(id);
        }

        return new EntityState<TEntity, TKey>(remaining, map);
    }

    /// <summary>Removes every entity.</summary>
    public EntityState<TEntity, TKey> RemoveAll(EntityState<TEntity, TKey> state)
        => EntityState<TEntity, TKey>.Empty;

    /// <summary>Replaces the entire collection with the supplied entities.</summary>
    public EntityState<TEntity, TKey> SetAll(EntityState<TEntity, TKey> state, IEnumerable<TEntity> entities)
    {
        var ids = new List<TKey>();
        var map = new Dictionary<TKey, TEntity>();
        foreach (var entity in entities)
        {
            var id = _selectId(entity);
            if (!map.ContainsKey(id))
                ids.Add(id);
            map[id] = entity;
        }

        return new EntityState<TEntity, TKey>(ids, map);
    }

    private static Dictionary<TKey, TEntity> ToDictionary(EntityState<TEntity, TKey> state)
        => new(state.Entities);
}
