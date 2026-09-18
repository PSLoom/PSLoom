// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.ObjectModel;
using System.Management.Automation.Runspaces;
using System.Reflection;

namespace PSLoom.TestKit;

/// <summary>
///   Creates real, opened runspaces and pipelines for hosted cmdlet tests.
/// </summary>
public static class PowerShellHost {
  /// <summary>
  ///   Creates and opens a runspace from <see cref="InitialSessionState.CreateDefault2" />.
  /// </summary>
  /// <param name="configure">Adds commands or state before the runspace opens.</param>
  public static Runspace CreateRunspace(Action<InitialSessionState>? configure = null) {
    var initialSessionState = InitialSessionState.CreateDefault2();
    configure?.Invoke(initialSessionState);

    var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
    runspace.Open();

    return runspace;
  }

  /// <summary>
  ///   Creates a pipeline bound to a runspace.
  /// </summary>
  public static PowerShell CreateShell(Runspace runspace) {
    var powerShell = PowerShell.Create();
    powerShell.Runspace = runspace;

    return powerShell;
  }

  /// <summary>
  ///   Registers every non-abstract <see cref="CmdletAttribute" /> type of an assembly, under its Verb-Noun name.
  /// </summary>
  public static void AddCmdletsFrom(this InitialSessionState initialSessionState, Assembly assembly) {
    ArgumentNullException.ThrowIfNull(initialSessionState);
    ArgumentNullException.ThrowIfNull(assembly);

    foreach (var type in assembly.GetTypes()) {
      if (!type.IsAbstract &&
          type.GetCustomAttribute<CmdletAttribute>() is { } cmdlet) {
        initialSessionState.Commands.Add(new SessionStateCmdletEntry($"{cmdlet.VerbName}-{cmdlet.NounName}", type, null));
      }
    }
  }

  /// <summary>
  ///   Makes a runspace the thread's <see cref="Runspace.DefaultRunspace" /> until disposed.
  /// </summary>
  public static IDisposable UseAsDefault(Runspace? runspace)
    => new DefaultRunspaceScope(runspace);

  /// <summary>
  ///   Runs a script and returns its output, clearing the pipeline's commands afterwards.
  /// </summary>
  public static Collection<PSObject> Run(this PowerShell powerShell, string script) {
    try {
      return powerShell.AddScript(script).Invoke();
    }
    finally {
      powerShell.Commands.Clear();
    }
  }

  private sealed class DefaultRunspaceScope : IDisposable {
    private readonly Runspace? _previous = Runspace.DefaultRunspace;

    public DefaultRunspaceScope(Runspace? runspace)
      => Runspace.DefaultRunspace = runspace;

    public void Dispose()
      => Runspace.DefaultRunspace = _previous;
  }
}
