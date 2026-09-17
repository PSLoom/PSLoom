namespace PSLoom.Warp.Dsl;

/// <summary>
///   A verb that can undo a previous top-level invocation that disappeared from the draft.
/// </summary>
public interface IRevertibleVerb {
  /// <summary>
  ///   Undoes <paramref name="previous" />. Called on a fresh, unbound instance of the verb.
  /// </summary>
  /// <param name="previous">The ledger entry of the invocation to undo.</param>
  void Revert(ReweaveEntry previous);
}