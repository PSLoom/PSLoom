// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Hosting;

namespace PSLoom.Runtime.PSReadLine;

/// <summary>
///   Checks what the loaded PSReadLine supports without ever throwing, so callers warn and no-op instead of surfacing a
///   <see cref="ParameterBindingException" />.
/// </summary>
/// <param name="commands">The command lookup of the session to probe.</param>
internal sealed class PSReadLineProbe(CommandInvocationIntrinsics commands) : IPSReadLineProbe {
  internal const string SET_OPTION_COMMAND = "Set-PSReadLineOption";

  /// <inheritdoc />
  public bool IsLoaded => ResolveSetOption() is not null;

  /// <inheritdoc />
  public bool HasOptionParameter(string parameterName) {
    ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

    try {
      return ResolveSetOption() is { } command && command.Parameters.ContainsKey(parameterName);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      return false;
    }
  }

  private CommandInfo? ResolveSetOption() {
    try {
      return commands.GetCommand(SET_OPTION_COMMAND, CommandTypes.Cmdlet | CommandTypes.Function);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      return null;
    }
  }
}
