namespace PersonalUniverse.Shared.Contracts.Events;

/// <summary>
/// Interface for subscribing to events from message queue
/// </summary>
public interface IEventSubscriber
{
    Task SubscribeAsync(string routingKey, Func<string, Task> handler);
    Task SubscribeAsync<T>(string routingKey, Func<T, Task> handler) where T : class;
}
