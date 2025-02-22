using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VitalRouter;

public readonly struct CharacterMoveCommand : ICommand
{
}

public readonly struct CharacterEnterCommand : ICommand
{
}

public readonly struct CharacterExitCommand : ICommand
{
}

public class LoggingInterceptor : ICommandInterceptor
{
    public async UniTask InvokeAsync<T>(
        T command,
        CancellationToken cancellation,
        Func<T, CancellationToken, UniTask> next)
        where T : ICommand
    {
        UnityEngine.Debug.Log($"start {GetType()} {command.GetType()}");
        await next(command, cancellation);
        UnityEngine.Debug.Log($"end {GetType()} {command.GetType()}");
    }
}

public class AInterceptor : ICommandInterceptor
{
    public UniTask InvokeAsync<T>(
        T command,
        CancellationToken cancellation,
        Func<T, CancellationToken, UniTask> next)
        where T : ICommand
    {
        return next(command, cancellation);
    }
}

public class BInterceptor : ICommandInterceptor
{
    public UniTask InvokeAsync<T>(
        T command,
        CancellationToken cancellation,
        Func<T, CancellationToken, UniTask> next)
        where T : ICommand
    {
        return next(command, cancellation);
    }
}

[Routes]
[Filter(typeof(AInterceptor))]
public partial class SamplePresenter
{
    public SamplePresenter()
    {
        UnityEngine.Debug.Log("SamplePresenter.ctor");
    }

    public UniTask On(CharacterEnterCommand cmd)
    {
        UnityEngine.Debug.Log("SamplePresenter.ctor");
        return default;
    }

    public UniTask On(CharacterMoveCommand cmd)
    {
        return default;
    }

    public void On(CharacterExitCommand cmd)
    {
    }
}

[Routes]
public partial class SamplePresenter2
{
    public UniTask On(CharacterEnterCommand cmd)
    {
        UnityEngine.Debug.Log($"{GetType()} {cmd.GetType()}");
        return default;
    }
}

[Routes]
public partial class SamplePresenter3
{
    public UniTask On(CharacterEnterCommand cmd)
    {
        UnityEngine.Debug.Log($"{GetType()} {cmd.GetType()}");
        return default;
    }
}

[Routes]
public partial class SamplePresenter4
{
    public UniTask On(CharacterEnterCommand cmd)
    {
        UnityEngine.Debug.Log($"{GetType()} {cmd.GetType()}");
        return default;
    }
}

[Routes]
public partial class SamplePresenter5
{
    public UniTask On(CharacterEnterCommand cmd)
    {
        UnityEngine.Debug.Log($"{GetType()} {cmd.GetType()}");
        return default;
    }
}
