// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace PSLoom.TestKit;

/// <summary>Creates in-process runspaces for tests that exercise cmdlets without importing a module.</summary>
public static class PowerShellHost {
  /// <summary>Opens a runspace.</summary>
  /// <param name="configure">Adds cmdlets or other entries to the session state before the runspace opens.</param>
  /// <returns>An open runspace that the caller disposes.</returns>
  public static Runspace CreateRunspace(Action<InitialSessionState>? configure = null) {
    var initialSessionState = InitialSessionState.CreateDefault2();
    configure?.Invoke(initialSessionState);

    var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
    runspace.Open();

    return runspace;
  }

  /// <summary>Creates a pipeline bound to a runspace.</summary>
  /// <param name="runspace">The runspace the pipeline runs in.</param>
  /// <returns>A <see cref="PowerShell" /> instance that the caller disposes.</returns>
  public static PowerShell CreateShell(Runspace runspace) {
    var powerShell = PowerShell.Create();
    powerShell.Runspace = runspace;

    return powerShell;
  }
}
