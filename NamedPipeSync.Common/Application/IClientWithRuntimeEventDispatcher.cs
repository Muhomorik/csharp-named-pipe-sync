using System;
using NamedPipeSync.Common.Domain.Events;

namespace NamedPipeSync.Common.Application;

/// <summary>
/// Application-level dispatcher that publishes domain events originating from the ClientWithRuntime aggregate.
/// </summary>
/// <remarks>
/// - Owns the observable stream for consumers across the application.
/// - Free of UI/process shutdown concerns; Presentation decides what to do with events.
/// </remarks>
public interface IClientWithRuntimeEventDispatcher
{
    /// <summary>
    /// Unified observable stream for all ClientWithRuntime domain events.
    /// </summary>
    IObservable<ClientWithRuntimeEvent> Events { get; }

    /// <summary>
    /// Publishes a new domain event to the Events stream.
    /// </summary>
    /// <param name="evt">Non-null domain event instance.</param>
    void Publish(ClientWithRuntimeEvent evt);
}
