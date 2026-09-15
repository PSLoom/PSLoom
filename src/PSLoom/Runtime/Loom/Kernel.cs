// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hosting;
using PSLoom.Warp.Kernel;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   The kernel behind <see cref="HarnessHost" />. Process-wide and stateless: all state lives in each runspace's
///   <see cref="LoomSession" />.
/// </summary>
internal sealed class Kernel : IKernel {
  private Kernel() { }

  public static Kernel Instance { get; } = new();

  /// <summary>
  ///   Attaches the kernel to the Warp facade. Safe to call repeatedly.
  /// </summary>
  public static void EnsureAttached()
    => HarnessHost.Attach(Instance);

  /// <inheritdoc />
  public void RegisterHarness(HarnessRegistration registration) {
    ArgumentNullException.ThrowIfNull(registration);

    var runspace = Runspace.DefaultRunspace ?? throw KernelException.NoRunspace();
    LoomSession.PerRunspace.For(runspace).Harnesses.Register(registration);
  }

  /// <inheritdoc />
  public IHarnessContext GetContext(Type harnessType)
    => LoomSession.PerRunspace.ForCurrent().Harnesses.GetContext(harnessType);

  /// <inheritdoc />
  public IVerbInvocation BeginVerb(LoomVerb verb)
    => LoomSession.PerRunspace.ForCurrent().BeginVerb(verb);
}
