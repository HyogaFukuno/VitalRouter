using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VitalRouter.Internal;

namespace VitalRouter;

public interface ICommandPublisher
{
    UniTask PublishAsync<T>(T command, CancellationToken cancellation = default) where T : ICommand;
}

public interface ICommandSubscribable
{
    Subscription Subscribe(ICommandSubscriber subscriber);
    Subscription Subscribe(IAsyncCommandSubscriber subscriber);
    void Unsubscribe(ICommandSubscriber subscriber);
    void Unsubscribe(IAsyncCommandSubscriber subscriber);
}

public interface ICommandSubscriber
{
    void Receive<T>(T command) where T : ICommand;
}

public interface IAsyncCommandSubscriber
{
    UniTask ReceiveAsync<T>(T command, CancellationToken cancellation = default) where T : ICommand;
}

public static class CommandPublisherExtensions
{
    public static void Enqueue<T>(
        this ICommandPublisher publisher,
        T command,
        CancellationToken cancellation = default)
        where T : ICommand
    {
        publisher.PublishAsync(command, cancellation).Forget();
    }
}

public sealed partial class Router : ICommandPublisher, ICommandSubscribable, IDisposable
{
    public static readonly Router Default = new();

    readonly FreeList<ICommandSubscriber> subscribers = new(8);
    readonly FreeList<IAsyncCommandSubscriber> asyncSubscribers = new(8);
    readonly FreeList<ICommandInterceptor> interceptors = new(8);

    bool disposed;

    readonly PublishCore publishCore;

    public Router(CommandOrdering ordering = CommandOrdering.Parallel)
    {
        publishCore = new PublishCore(this);
        Filter(ordering);
    }

    public UniTask PublishAsync<T>(T command, CancellationToken cancellation = default) where T : ICommand
    {
        CheckDispose();

        if (HasInterceptor())
        {
            return PublishWithInterceptorsAsync(command, cancellation);
        }
        return publishCore.ReceiveAsync(command, cancellation);
    }

    public Subscription Subscribe(ICommandSubscriber subscriber)
    {
        subscribers.Add(subscriber);
        return new Subscription(this, subscriber);
    }

    public Subscription Subscribe(IAsyncCommandSubscriber subscriber)
    {
        asyncSubscribers.Add(subscriber);
        return new Subscription(this, subscriber);
    }

    public void Unsubscribe(ICommandSubscriber subscriber)
    {
        subscribers.Remove(subscriber);
    }

    public void Unsubscribe(IAsyncCommandSubscriber subscriber)
    {
        asyncSubscribers.Remove(subscriber);
    }

    public void UnsubscribeAll()
    {
        subscribers.Clear();
        asyncSubscribers.Clear();
        interceptors.Clear();
    }

    public Router Filter(ICommandInterceptor interceptor)
    {
        interceptors.Add(interceptor);
        return this;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            subscribers.Clear();
            asyncSubscribers.Clear();
            interceptors.Clear();
        }
    }

    async UniTask PublishWithInterceptorsAsync<T>(T command, CancellationToken cancellation = default) where T : ICommand
    {
        var context = InvokeContextWithFreeList<T>.Rent(interceptors, publishCore);
        try
        {
            await context.InvokeRecursiveAsync(command, cancellation);
        }
        finally
        {
            context.Return();
        }
    }

    bool HasInterceptor()
    {
        foreach (var interceptorOrNull in interceptors.AsSpan())
        {
            if (interceptorOrNull != null)
            {
                return true;
            }
        }
        return false;
    }

    void CheckDispose()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(UniTaskAsyncLock));
        }
    }

    class PublishCore : IAsyncCommandSubscriber
    {
        readonly Router source;
        readonly ReusableWhenAllSource whenAllSource = new();
        readonly ExpandBuffer<UniTask> executingTasks = new(8);

        public PublishCore(Router source)
        {
            this.source = source;
        }

        public UniTask ReceiveAsync<T>(T command, CancellationToken cancellation) where T : ICommand
        {
            try
            {
                foreach (var sub in source.subscribers.AsSpan())
                {
                    sub?.Receive(command);
                }

                foreach (var sub in source.asyncSubscribers.AsSpan())
                {
                    if (sub != null)
                    {
                        var task = sub.ReceiveAsync(command, cancellation);
                        executingTasks.Add(task);
                    }
                }

                if (executingTasks.Count > 0)
                {
                    whenAllSource.Reset(executingTasks);
                    return whenAllSource.Task;
                }

                return UniTask.CompletedTask;
            }
            finally
            {
                executingTasks.Clear();
            }
        }
    }
}