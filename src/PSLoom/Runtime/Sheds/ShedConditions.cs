// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   Evaluates <c>-LoadIf</c> and <c>-RequiresCommand</c>, and runs <c>-AtLoad</c>, recording failures on a run.
/// </summary>
internal static class ShedConditions {
  public static bool Hold(LoomSession session, ShedEntry entry, LoomRun run) {
    var declaration = entry.Declaration;

    try {
      if ((declaration.RequiresCommand is { } command &&
           session.Engine?.InvokeCommand.GetCommand(command, CommandTypes.All) is null) ||
          (declaration.LoadIf is { } condition &&
           !LanguagePrimitives.IsTrue(condition.InvokeReturnAsIs()))) {
        entry.Complete(ShedState.Skipped, TimeSpan.Zero, []);
        return false;
      }

      return true;
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      Fail(entry, run, exception);
      return false;
    }
  }

  public static void RunAtLoad(LoomSession session, ShedEntry entry, LoomRun run) {
    if (entry.Declaration.AtLoad is not { } action) {
      return;
    }

    try {
      action.InvokeReturnAsIs();
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      Fail(entry, run, exception);
    }
  }

  private static void Fail(ShedEntry entry, LoomRun run, Exception exception) {
    var error = LoomException.ShedApplyFailed(entry.Line, entry.Statement, exception).ToErrorRecord();
    run.Report(error);
    entry.Complete(ShedState.Failed, entry.Elapsed, [.. entry.Errors, error]);
  }
}
