namespace Blex.Demo.Stores;

/// <summary>An order placed in the cross-store coordination demo.</summary>
public sealed record Order(int Id, string Item, int Quantity);

/// <summary>
/// The <em>source</em> store in the cross-store demo: something else listens for its actions and
/// reacts. It knows nothing about its listeners -- coordination is wired through the manager.
/// </summary>
[Store(Name = "orders")]
public partial class OrdersStore
{
    private int _nextId = 1;

    [State] private IReadOnlyList<Order> _orders = [];

    [Computed] private int ComputeCount() => Orders.Count;

    [Computed] private int ComputeUnits() => Orders.Sum(o => o.Quantity);

    [Action]
    private void OnPlace(string item, int quantity)
        => Orders = [.. Orders, new Order(_nextId++, item, quantity)];

    [Action] private void OnCancelLast()
    {
        if (Orders.Count > 0)
            Orders = [.. Orders.Take(Orders.Count - 1)];
    }

    [Action] private void OnClear() => Orders = [];
}

/// <summary>
/// The <em>reacting</em> store in the cross-store demo. A subscription on the manager feeds it;
/// it has no reference to <see cref="OrdersStore"/>.
/// </summary>
[Store(Name = "notifications")]
public partial class NotificationsStore
{
    [State] private IReadOnlyList<string> _messages = [];

    [Computed] private int ComputeUnread() => Messages.Count;

    [Action] private void OnPush(string message) => Messages = [message, .. Messages];

    [Action] private void OnClear() => Messages = [];
}
