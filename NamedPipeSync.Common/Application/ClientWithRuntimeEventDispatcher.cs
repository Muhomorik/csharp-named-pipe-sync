using System;
using System.Reactive.Subjects;
using NamedPipeSync.Common.Domain.Events;
using NLog;

namespace NamedPipeSync.Common.Application;

/// <summary>
/// Default in-process dispatcher for <see cref="ClientWithRuntimeEvent"/>.
/// </summary>
public sealed class ClientWithRuntimeEventDispatcher : IClientWithRuntimeEventDispatcher, IDisposable
{
    private readonly ILogger _logger;
    private readonly Subject<ClientWithRuntimeEvent> _subject = new();
    private bool _disposed;

    public ClientWithRuntimeEventDispatcher(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IObservable<ClientWithRuntimeEvent> Events => _subject;

    public void Publish(ClientWithRuntimeEvent evt)
    {
        if (evt is null) throw new ArgumentNullException(nameof(evt));
        if (_disposed) return;

        try
        {
            _subject.OnNext(evt);
            
            _logger.Trace("Published event: {0}", evt);
        }
        catch (Exception ex)
        {
            // Log the exception object directly, per guidelines.
            _logger.Error(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _subject.OnCompleted();
        _subject.Dispose();
    }
}
