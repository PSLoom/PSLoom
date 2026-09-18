namespace PSLoom.Warp.Hooks;

/// <summary>
///   Subscribes to lifecycle events. A kind is wired the first time anything subscribes to it.
/// </summary>
public interface IHookBus {
  /// <summary>
  ///   Subscribes a handler.
  /// </summary>
  /// <param name="kind">The event kind.</param>
  /// <param name="handler">The handler.</param>
  /// <param name="name">An optional name shown by <c>Get-Hook</c> and <c>Trace-Hook</c>.</param>
  /// <returns>A handle that unsubscribes when disposed. Wiring itself is never unwound.</returns>
  IDisposable Subscribe(HookKind kind, HookHandler handler, string? name = null);
}
