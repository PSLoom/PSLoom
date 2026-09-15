// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using PSLoom.Runtime;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Styles;
using PSLoom.TestKit;

namespace PSLoom.Tests.Utility;

/// <summary>
///   A runspace with every kernel cmdlet registered, and a pipeline on it.
/// </summary>
internal sealed class KernelSession : IDisposable {
  public KernelSession() {
    // Test runspaces host kernel cmdlets directly instead of importing the module, so attach the kernel as the module would.
    Kernel.EnsureAttached();

    Runspace = PowerShellHost.CreateRunspace(state => state.AddCmdletsFrom(typeof(KernelException).Assembly));
    Shell = PowerShellHost.CreateShell(Runspace);
  }

  public LoomSession Loom => LoomSession.PerRunspace.For(Runspace);

  public object? Style(string context, string name)
    => StyleStore.PerRunspace.For(Runspace).Resolve(context, name)?.Value;

  public Runspace Runspace { get; }

  public PowerShell Shell { get; }

  public PSDataStreams Streams => Shell.Streams;

  public Collection<PSObject> Run(string script)
    => Shell.Run(script);

  public object? Global(string name)
    => Runspace.SessionStateProxy.GetVariable(name);

  public EngineIntrinsics Engine()
    => (EngineIntrinsics)Run("$ExecutionContext")[0].BaseObject;

  public void Dispose() {
    Shell.Dispose();
    Runspace.Dispose();
  }
}
