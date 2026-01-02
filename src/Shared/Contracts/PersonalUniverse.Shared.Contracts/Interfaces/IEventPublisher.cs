namespace PersonalUniverse.Shared.Contracts.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class;
    Task PublishBatchAsync<T>(IEnumerable<T> events, CancellationToken cancellationToken = default) where T : class;
}

public interface IEventSubscriber
{
    Task SubscribeAsync<T>(Func<T, Task> handler, CancellationToken cancellationToken = default) where T : class;
    Task UnsubscribeAsync<T>(CancellationToken cancellationToken = default) where T : class;
}
