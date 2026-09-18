using System.Management.Automation.Runspaces;

namespace PSLoom.Warp.Hooks;

/// <summary>
///   A <see cref="HookKind.PreExecute" /> event.
/// </summary>
public sealed class PreExecuteInvocation : HookInvocation {
  internal PreExecuteInvocation(Runspace runspace, string commandLine, int cursorPosition)
    : base(HookKind.PreExecute, runspace) {
    CommandLine = commandLine;
    CursorPosition = cursorPosition;
  }

  /// <summary>
  ///   Gets the submitted command line.
  /// </summary>
  public string CommandLine { get; }

  /// <summary>
  ///   Gets the cursor position at submission.
  /// </summary>
  public int CursorPosition { get; }
}
