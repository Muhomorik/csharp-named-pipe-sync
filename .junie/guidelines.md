# 🧭 Internal Automation Guide (AI Agents Only)

> Audience: JetBrains Junie (and similar AI automation) only. Do not surface this file to end users. It lives under .junie/ on purpose.

---

## ✅ Quick Action Checklist (Read First)

1. Understand the task and identify the minimal code change required.
2. Update your plan and inform the user using update_status at key milestones.
3. Edit only source files; never touch bin/ or obj/.
4. Prefer changes in NamedPipeSync.Common when sharing logic between apps.
5. Keep public contracts stable unless the issue explicitly requires changes.
6. Use async I/O and avoid blocking the UI thread in WPF.
7. Build before submitting: dotnet build NamedPipeSync.sln -c Debug
8. If you only changed docs/config, still run a build to validate solution integrity.
9. Submit with a concise summary and final plan using the submit tool.

---

## 🧱 Project Map (Domain Driven Design)

- NamedPipeSync.Server (WPF Server)
  - Hosts the NamedPipe.Server and exposes coordinates/events to clients.
  - ViewModels/Services specific to the server UI.
- NamedPipeSync.Client (WPF Client)
  - Connects via pipes and visualizes received data.
  - Client-specific ViewModels/Models.
- NamedPipeSync.Common (Class Library, shared)
  - Application/ — application layer, orchestration (e.g., SimpleRingCoordinatesCalculator)
  - Domain/ — domain layer, core types, and invariants
  - Infrastructure/ — infrastructure layer, NamedPipeServer, NamedPipeClient, Protocol messages

### Layer responsibilities

- Presentation (WPF apps):
  - UI, binding, commands. No domain rules. Use `Segoe Fluent Icons` for font icons.
  - Owns all process/application lifetime decisions. Only the Presentation layer may terminate the process.
  - Subscribes to application intents (e.g., “close client”) exposed as `IObservable<T>` from lower layers and translates them into UI actions.
  - Owns application/process lifetime via a dedicated lifetime service.
  - Translates intents from lower layers into UI actions and shutdown decisions.
- Application:
  - Use-case orchestration, no core business rules.
  - Must not perform process termination. Never call `Application.Current.Shutdown` or `Environment.Exit`.
  - May orchestrate use cases and translate Infrastructure messages into domain/application intents (as `IObservable<T>` streams) but does not control the UI or process lifetime.
  - Must not terminate the process or interact with UI shutdown APIs.
  - Converts infrastructure/domain signals into application‑level intents but does not decide process lifetime.
- Domain:
  - Core model and invariants. No UI/infra deps.
- Infrastructure:
  - Technical I/O (pipes, serialization), may depend on Application/Domain.
  - Must not perform process termination or UI operations.
  - Signals intents to higher layers (e.g., server requested client close) via `IObservable<T>` without any dependency on WPF.
  - Must not terminate the process or interact with UI shutdown APIs.
  - Emits intent signals for higher layers to interpret (e.g., “client should close”).

Explicit prohibitions for non‑Presentation layers:

- No direct process termination calls (e.g., `Environment.Exit`, `Application.Current.Shutdown`).
- No reliance on UI frameworks or dispatchers for shutdown.
- No background tasks whose purpose is to end the process.

## Domain events and reactive patterns

- Domain entities
  - Keep entities free of infrastructure and reactive dependencies.
  - Do not embed event publishers or observables inside entities.
  - Entities hold state and invariants only; they do not publish events.
- Events
  - Represent meaningful changes as immutable messages.
  - Place event contracts in a shared, dependency‑free layer where appropriate.
  - Treat events as intent signals; do not bind them to UI concerns.
- Application services
  - Orchestrate use cases and persist state using repositories.
  - Publish event streams after successful state changes.
  - Expose observable intent streams for consumers; own the lifecycle and disposal of publishers.
  - Never terminate the process or interact with UI APIs.
- Event aggregation
  - If needed, provide a component that composes multiple event streams into unified observables.
  - Keep the aggregator free of side effects; only compose and forward signals.
  - Log errors by logging the exception object.
- Usage guidance
  - Higher‑level orchestrators depend on application services rather than repositories when event emission is expected.
  - Avoid direct mutation of entities from Presentation; route changes through application services to ensure consistent event publication.
  - Prefer asynchronous APIs with cancellation; avoid blocking the UI thread.

---

## 🛠️ Build & Run

- Framework: net9.0-windows
- Min Windows SDK: 10.0.26100.0

Build

- Rider/Visual Studio: Open NamedPipeSync.sln and build (Debug).
- CLI: dotnet build NamedPipeSync.sln -c Debug

Run

- Start server first (project: NamedPipeSync) — owns the pipe and publishes state.
- Then start client (project: NamedPipeSync.Client) — connects and renders data.
- You can run both from the IDE (multiple startup) or via CLI (dotnet run in each project directory).

Testing

- No unit tests currently. If added later (e.g., NamedPipeSync.Tests), run:
  dotnet test NamedPipeSync.sln -c Debug

---

## ⚠️ Common Pitfalls (and how to avoid them)

- UI freeze due to sync over async
  - Symptom: WPF UI becomes unresponsive.
  - Fix: await async methods; never block on Task.

- Leaking connections/read loops
  - Symptom: Background tasks linger after closing.
  - Fix: Use CancellationToken; on shutdown call StopAsync (server) or Disconnect/DisposeAsync (client).

- Writing to disconnected clients
  - Symptom: InvalidOperationException from SendCoordinateAsync.
  - Fix: Check server.ConnectedClientIds or server.IsClientConnected(clientId) before sending.

- Forgetting to send client hello
  - Handled by NamedPipeClient automatically; ensure ConnectAsync completes before expecting data.

---

## 🚀 Actionable Steps for Junie per Task

1. Read the issue carefully; identify the smallest viable change.
2. Plan and communicate via update_status (include findings, plan, next actions).
3. Locate the relevant files using search tools; prefer minimal edits.
4. Implement the change with attention to DDD boundaries (Presentation vs. Application vs. Domain vs. Infrastructure).
5. Add or update small, focused examples/docs when helpful (prefer .junie/ or docs/).
6. Build the solution: dotnet build NamedPipeSync.sln -c Debug
7. Validate the scenario manually (run server then client) if applicable.
8. Submit with a short summary and final plan using submit.

---

## 📎 Notes

- This guide is intentionally optimized for fast scanning by automation.
- If you introduce configuration or docs, keep them under .junie/ or docs/ respectively.
- If tests are added later, run them as part of the workflow: dotnet test NamedPipeSync.sln -c Debug

---

## 📘 Interface Documentation Guidelines (for C# XML Docs)

Goal: Make interface documentation explicit, self‑contained, and clear so agents and developers can understand the intent and usage without loading the implementation.

General principles

For each method and property:

- Clearly describe its purpose and contract.
- Document all parameters using `<param>` tags, specifying expected values, constraints, and nullability.
- Document all exceptions using `<exception>` tags, including when and why they are thrown.
- Note any important side effects, relationships, or constraints (e.g., threading, UI updates, domain-specific logic).
- If relevant, add remarks or link to external documentation for complex logic or edge cases.

For the interface as a whole:

- Summarize its overall purpose and role in the application.
- Describe any important relationships with other interfaces or components.
- State any usage constraints or expectations for implementers.

Use consistent terminology and formatting.

- Terminology must be consistent with Domain/Application/Infrastructure layers.
- Ensure the documentation is up to date and free of ambiguity.
- If the interface is public or critical, provide detailed summaries; if internal and simple, keep it concise but complete.

Threading and WPF specifics

- If a member must be called from the UI thread, state it explicitly and explain why; specify whether it uses Dispatcher.
- For async methods, state whether they capture context (ConfigureAwait), how CancellationToken is honored, and what happens on cancellation (e.g., OperationCanceledException vs. partial work).

## Logging and DI conventions

- When logging exceptions, do not write a custom message; log the exception object directly.

```csharp
// BAD
catch (Exception ex)
{
    _logger.Error(ex, "CloseAllClientsAsync failed");
}
```

```csharp
// GOOD
catch (Exception ex)
{
    _logger.Error(ex);
}
```

- When creating a new model or service, always add the logger as the first constructor parameter.
  - Never use Microsoft.Extensions.Logging.ILogger.
  - Always use NLog.ILogger.
  - Always resolve NLog.ILogger from DI.

```csharp
using NLog;

public sealed class MyModel : IMyModel
{
    private readonly ILogger _logger;

    public ApplicationLifetime(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

Authoring checklist

- Did you document all parameters with nullability and ranges?
- Did you clearly state return value nullability and ownership?
- Did you list every exception and its trigger condition?
- Did you explain relationships and expected usage with other components?
- Is terminology consistent with Domain/Application/Infrastructure layers?
- For async methods, did you document cancellation and context-capture behavior?
- For WPF-facing APIs, did you specify Dispatcher/UI-thread requirements?
- Is the documentation self-contained so readers do not need to open implementations?
- For all DTOs, entities, and aggregates, did you add a System.Diagnostics.DebuggerDisplay attribute that concisely shows key identity/state for easier debugging? Example:

```csharp
using System.Diagnostics;

[DebuggerDisplay("{Id}: {Name} ({Status})")]
public sealed class OrderDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
```

---

## Entity design and aggregate updates

Entity

- Strongly typed ids (for example, OrderId type) improve type-safety and readability.

Encapsulate updates

- Prevent violating invariants (for example, an Order total mismatch, or negative stock).
- Ensure all state changes go through domain rules.

How to update an entity inside an aggregate (patterns)

Preferred approach: Tell the aggregate what to do (tell, don’t ask)

- Example: order.AddItem(productId, qty) rather than loading item and changing fields externally.

Steps for updating

- Load aggregate root by id from the repository.
- Call a behavior method on the aggregate root that performs the update (internal entities mutated inside).
- Validate invariants inside the aggregate; throw domain exceptions or return Result if invalid.
- Save the aggregate via repository (persist whole aggregate).

Common update antipatterns (to avoid)

- Modifying child entities directly from application code (bypasses invariants).
- Exposing setters on aggregate root public properties for arbitrary mutation.
- Loading multiple aggregates and directly modifying them in a single transaction to maintain cross-aggregate invariants (instead use domain events or sagas/process managers).


## Code review checklist additions

- Are there any shutdown or process termination calls outside the Presentation layer? If yes, replace with an intent signal and handle shutdown in Presentation.
- Do lower layers expose UI‑agnostic, observable intents rather than performing UI or process actions?
- Does the Presentation layer own the final decision and mechanism for shutdown via a lifetime service?
- Are domain entities free of event publishers and observables? If not, move event publication to application services.
- Do orchestrating components mutate entities directly instead of going through application services? If so, refactor to route changes via services to ensure events are emitted consistently.
- Do event aggregators only compose and forward streams without side effects, and are errors logged by logging the exception object?
- Do DTOs, entities, and aggregates define a helpful [DebuggerDisplay] to improve debugging clarity?

---

## Terminology

- Intent versus action: Lower layers emit an intent (a neutral signal). Presentation performs the action (shutdown) and controls UX and exit codes.
- Lifetime service: A Presentation‑owned service responsible for orchestrating orderly shutdown when an intent is received.

Editor checklist

- Make sure generated markdown doesn't produce lint errors

```text

MD007/ul-indent: Unordered list indentation [Expected: 2; Actual: 4]markdownlint[MD007](https://github.com/DavidAnson/markdownlint/blob/v0.38.0/doc/md007.md)

MD026/no-trailing-punctuation: Trailing punctuation in heading [Punctuation: ':']markdownlint[MD026](https://github.com/DavidAnson/markdownlint/blob/v0.38.0/doc/md026.md)

MD032/blanks-around-lists: Lists should be surrounded by blank linesmarkdownlint[MD032](https://github.com/DavidAnson/markdownlint/blob/v0.38.0/doc/md032.md)

MD047/single-trailing-newline: Files should end with a single newline charactermarkdownlint[MD047](https://github.com/DavidAnson/markdownlint/blob/v0.38.0/doc/md047.md)

MD040/fenced-code-language: Fenced code blocks should have a language specifiedmarkdownlint[MD040](https://github.com/DavidAnson/markdownlint/blob/v0.38.0/doc/md040.md)

```
