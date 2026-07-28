namespace Blex.Demo.Stores;

/// <summary>An order placed in the cross-store coordination demo.</summary>
public sealed record Order(int Id, string Item, int Quantity);

/// <summary>
/// The <em>source</em> store in the cross-store demo: something else listens for its actions and
/// reacts. It knows nothing about its listeners -- coordination is wired through the manager.
/// </summary>
[StoreAttributeBlex(Name = "orders")]
public partial class OrdersStore
{
    private int _nextId = 1;

    [StateAttributeBlex] private IReadOnlyList<Order> _orders = [];

    [ComputedAttributeBlex] private int ComputeCount() => Orders.Count;

    [ComputedAttributeBlex] private int ComputeUnits() => Orders.Sum(o => o.Quantity);

    [ActionAttributeBlex]
    private void OnPlace(string item, int quantity)
        => Orders = [.. Orders, new Order(_nextId++, item, quantity)];

    [ActionAttributeBlex] private void OnCancelLast()
    {
        if (Orders.Count > 0)
            Orders = [.. Orders.Take(Orders.Count - 1)];
    }

    [ActionAttributeBlex] private void OnClear() => Orders = [];
}

/// <summary>
/// The <em>reacting</em> store in the cross-store demo. A subscription on the manager feeds it;
/// it has no reference to <see cref="OrdersStore"/>.
/// </summary>
[StoreAttributeBlex(Name = "notifications")]
public partial class NotificationsStore
{
    [StateAttributeBlex] private IReadOnlyList<string> _messages = [];

    [ComputedAttributeBlex] private int ComputeUnread() => Messages.Count;

    [ActionAttributeBlex] private void OnPush(string message) => Messages = [message, .. Messages];

    [ActionAttributeBlex] private void OnClear() => Messages = [];
}
