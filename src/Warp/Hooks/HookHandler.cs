namespace PSLoom.Warp.Hooks;

/// <summary>
///   A hook handler. Must not assume it runs alone: other handlers run for the same event regardless of its outcome.
/// </summary>
/// <param name="invocation">The event.</param>
public delegate void HookHandler(HookInvocation invocation);
