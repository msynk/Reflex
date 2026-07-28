using System.Text.Json.Serialization;

namespace Blex;

/// <summary>
/// An immutable, normalized collection: an ordered list of ids plus an id-keyed entity map.
/// Designed to be stored as a single <c>[StateAttributeBlex]</c> field. Every mutation returns a new instance,
/// so change detection (and therefore notifications and time-travel) works correctly. Create and
/// mutate instances through an <see cref="EntityAdapterBlex{TEntity, TKey}"/>.
/// </summary>
/// <typeparam name="TEntity">The stored entity type.</typeparam>
/// <typeparam name="TKey">The id type (must be non-null and JSON-serializable as a dictionary key).</typeparam>
public sealed class EntityStateBlex<TEntity, TKey>
    where TKey : notnull
{
    /// <summary>Creates a normalized state from an ordered id list and matching entity map.</summary>
    /// <remarks>
    /// This is also the JSON constructor. A persisted payload that is missing (or nulls out) either
    /// half degrades to an empty collection rather than producing an instance that throws on first
    /// enumeration.
    /// </remarks>
    [JsonConstructor]
    public EntityStateBlex(IReadOnlyList<TKey> ids, IReadOnlyDictionary<TKey, TEntity> entities)
    {
        Ids = ids ?? EmptyIds;
        Entities = entities ?? EmptyEntities;
    }

    private static readonly IReadOnlyList<TKey> EmptyIds = Array.Empty<TKey>();

    private static readonly IReadOnlyDictionary<TKey, TEntity> EmptyEntities =
        new System.Collections.ObjectModel.ReadOnlyDictionary<TKey, TEntity>(new Dictionary<TKey, TEntity>());

    /// <summary>The ids in insertion/display order.</summary>
    public IReadOnlyList<TKey> Ids { get; }

    /// <summary>The id-to-entity map.</summary>
    public IReadOnlyDictionary<TKey, TEntity> Entities { get; }

    /// <summary>An empty state. Shared and defensively read-only.</summary>
    public static EntityStateBlex<TEntity, TKey> Empty { get; } = new(EmptyIds, EmptyEntities);

    /// <summary>Number of stored entities.</summary>
    [JsonIgnore]
    public int Count => Ids.Count;

    /// <summary>Entities in id order.</summary>
    /// <remarks>
    /// Lazily enumerated: the sequence is walked on each iteration rather than materialized, so
    /// cache it (<c>ToList()</c>) when a render pass reads it more than once. Every adapter
    /// operation keeps <see cref="Ids"/> and <see cref="Entities"/> in step; a hand-edited payload
    /// that lists an id with no matching entity throws <see cref="KeyNotFoundException"/> here.
    /// </remarks>
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
