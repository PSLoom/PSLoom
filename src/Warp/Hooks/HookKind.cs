namespace PSLoom.Warp.Hooks;

/// <summary>
///   Lifecycle events a harness can subscribe to.
/// </summary>
public enum HookKind {
  /// <summary>The current location changed (<c>chpwd</c>).</summary>
  DirectoryChanged,

  /// <summary>Before the prompt is drawn (<c>precmd</c>).</summary>
  PrePrompt,

  /// <summary>A command line was genuinely submitted (<c>preexec</c>). Requires the PSReadLine fork.</summary>
  PreExecute,

  /// <summary>Command lookup failed (<c>command_not_found_handler</c>).</summary>
  CommandNotFound,

  /// <summary>The engine is idle with an empty buffer.</summary>
  Idle,

  /// <summary>The draft finished, or the first prompt is about to be drawn when there is no draft.</summary>
  SessionStarting,

  /// <summary>The session is exiting (<c>zshexit</c>).</summary>
  SessionExiting
}
