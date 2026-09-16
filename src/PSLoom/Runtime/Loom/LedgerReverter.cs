// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   Undoes verb invocations a reweave removed: reverts the revertible ones and warns once about those that need a restart.
/// </summary>
internal static class LedgerReverter {
  public static void Revert(IEnumerable<LedgerItem> removed, LoomRun run, PSCmdlet cmdlet) {
    ArgumentNullException.ThrowIfNull(removed);
    ArgumentNullException.ThrowIfNull(run);
    ArgumentNullException.ThrowIfNull(cmdlet);

    var requiresRestart = new List<string>();

    foreach (var item in removed) {
      if (!item.Verb.IsRevertible) {
        requiresRestart.Add(ReweaveFingerprint.Describe(item));
        continue;
      }

      try {
        var verb = (IRevertibleVerb)Activator.CreateInstance(item.Verb.VerbType)!;
        verb.Revert(item.Entry);
        cmdlet.WriteVerbose($"Undid removed statement: {ReweaveFingerprint.Describe(item)}.");
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
        run.Report(LoomException.ReweaveRevertFailed(item.Entry.VerbName, exception).ToErrorRecord());
      }
    }

    if (requiresRestart.Count == 0) {
      return;
    }

    requiresRestart.Reverse();
    cmdlet.WriteWarning(LoomException.ReweaveRequiresRestart(requiresRestart));
  }
}
