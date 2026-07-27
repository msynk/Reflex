namespace Blex.Demo.Stores;

/// <summary>A normalized entity stored in <see cref="ContactsStore"/>.</summary>
public sealed record Contact(int Id, string Name, string Team, bool Starred);

/// <summary>
/// Normalized collection state via <see cref="EntityAdapter{TEntity, TKey}"/>. The adapter is a
/// static field (it holds the id selector and is not serialized); the <c>[State]</c> field holds
/// the immutable <see cref="EntityState{TEntity, TKey}"/> that every operation returns anew.
/// </summary>
[Store(Name = "contacts")]
public partial class ContactsStore
{
    /// <summary>Sorted by name, so <c>Ids</c> stays ordered after every operation.</summary>
    private static readonly EntityAdapter<Contact, int> Adapter =
        new(c => c.Id, Comparer<Contact>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name)));

    private int _nextId = 5;

    [State] private EntityState<Contact, int> _contacts = Adapter.GetInitialState(
    [
        new(1, "Ada Lovelace", "Research", true),
        new(2, "Grace Hopper", "Compilers", false),
        new(3, "Alan Turing", "Research", false),
        new(4, "Barbara Liskov", "Languages", true),
    ]);

    [State] private string _teamFilter = "All";

    [Computed] private int ComputeTotal() => Contacts.Count;

    [Computed] private int ComputeStarred() => Contacts.All.Count(c => c.Starred);

    [Computed]
    private IReadOnlyList<Contact> ComputeVisible()
        => TeamFilter == "All"
            ? [.. Contacts.All]
            : [.. Contacts.All.Where(c => c.Team == TeamFilter)];

    [Computed]
    private IReadOnlyList<string> ComputeTeams()
        => ["All", .. Contacts.All.Select(c => c.Team).Distinct().Order()];

    [Action] private void OnSetTeamFilter(string team) => TeamFilter = team;

    [Action]
    private void OnAdd(string name, string team)
        => Contacts = Adapter.AddOne(Contacts, new Contact(_nextId++, name, team, false));

    [Action]
    private void OnToggleStar(int id)
        => Contacts = Adapter.UpdateOne(Contacts, id, c => c with { Starred = !c.Starred });

    [Action] private void OnRemove(int id) => Contacts = Adapter.RemoveOne(Contacts, id);

    [Action]
    private void OnStarAll()
        => Contacts = Adapter.Map(Contacts, c => c with { Starred = true });

    [Action]
    private void OnRemoveUnstarred()
        => Contacts = Adapter.RemoveMany(Contacts, [.. Contacts.All.Where(c => !c.Starred).Select(c => c.Id)]);

    [Action] private void OnRemoveAll() => Contacts = Adapter.RemoveAll(Contacts);

    [Action]
    private void OnReseed()
    {
        _nextId = 5;
        Contacts = Adapter.SetAll(Contacts,
        [
            new(1, "Ada Lovelace", "Research", true),
            new(2, "Grace Hopper", "Compilers", false),
            new(3, "Alan Turing", "Research", false),
            new(4, "Barbara Liskov", "Languages", true),
        ]);
    }
}
