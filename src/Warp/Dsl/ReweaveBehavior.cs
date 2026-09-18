namespace PSLoom.Warp.Dsl;

/// <summary>
///   How <c>Invoke-Loom -Reweave</c> treats a top-level verb whose invocation changed or disappeared.
/// </summary>
public enum ReweaveBehavior {
  /// <summary>
  ///   Idempotent upsert: a changed invocation runs again; a removed one is reverted when the verb implements
  ///   <see cref="IRevertibleVerb" />.
  /// </summary>
  Replay,

  /// <summary>
  ///   Cannot be undone: a new invocation runs, a removed one is reported as requiring a restart.
  /// </summary>
  Additive
}