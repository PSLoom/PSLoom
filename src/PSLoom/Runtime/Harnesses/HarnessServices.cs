// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Styles;
using PSLoom.Runtime.Treadles;
using PSLoom.Runtime.Verbs;
using PSLoom.Warp.Diagnostics;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hooks;
using PSLoom.Warp.Hosting;
using PSLoom.Warp.Kernel;
using PSLoom.Warp.Storage;
using PSLoom.Warp.Styles;
using PSLoom.Warp.Treadles;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   The capabilities of one composed harness in one runspace: its builder during <c>Compose</c>, its context afterwards.
/// </summary>
internal sealed class HarnessServices : IHarnessBuilder, IHarnessContext {
  private readonly Lazy<IHarnessStorage> _storage;

  public HarnessServices(LoomSession session, HarnessRegistration registration) {
    var assemblyName = registration.HarnessType.Assembly.GetName();
    var name = registration.Attribute.Name;

    HarnessType = registration.HarnessType;
    Identity = new HarnessIdentity(name, registration.Attribute.Description, assemblyName.Name ?? name, assemblyName.Version ?? new Version(0, 0),
      registration.CompiledWarpVersion);
    Verbs = new OwnedVerbRegistry(session.Verbs, name);
    Styles = StyleStore.PerRunspace.For(session.Runspace);
    Hooks = HookBus.PerRunspace.For(session.Runspace);
    Diagnostics = DiagnosticsFactory.Instance;
    PSReadLine = new SessionPSReadLineProbe(session);
    Harnesses = new HarnessDirectoryView(session.Directory, name);
    Treadles = TreadleCatalog.PerRunspace.For(session.Runspace);
    Dsl = new DslRunner(session);
    _storage = new Lazy<IHarnessStorage>(() => new HarnessStorage(CreelRoot.ForHarness(name)));
  }

  public Type HarnessType { get; }

  /// <inheritdoc />
  public HarnessIdentity Identity { get; }

  /// <inheritdoc />
  public IVerbRegistry Verbs { get; }

  /// <inheritdoc />
  public IStyleStore Styles { get; }

  /// <inheritdoc />
  public IHookBus Hooks { get; }

  /// <inheritdoc />
  public IHarnessStorage Storage => _storage.Value;

  /// <inheritdoc />
  public IDiagnostics Diagnostics { get; }

  /// <inheritdoc />
  public IPSReadLineProbe PSReadLine { get; }

  /// <inheritdoc />
  public IHarnessDirectory Harnesses { get; }

  /// <inheritdoc />
  public ITreadleCatalog Treadles { get; }

  /// <inheritdoc />
  public IDslRunner Dsl { get; }
}
