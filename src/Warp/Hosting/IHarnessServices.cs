// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Diagnostics;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hooks;
using PSLoom.Warp.Storage;
using PSLoom.Warp.Styles;
using PSLoom.Warp.Treadles;

namespace PSLoom.Warp.Hosting;

/// <summary>
///   The kernel capabilities a harness uses, scoped to that harness and to one runspace.
/// </summary>
public interface IHarnessServices {
  /// <summary>
  ///   Gets the harness identity.
  /// </summary>
  HarnessIdentity Identity { get; }

  /// <summary>
  ///   Gets the verb registry.
  /// </summary>
  IVerbRegistry Verbs { get; }

  /// <summary>
  ///   Gets the style store.
  /// </summary>
  IStyleStore Styles { get; }

  /// <summary>
  ///   Gets the hook bus.
  /// </summary>
  IHookBus Hooks { get; }

  /// <summary>
  ///   Gets the harness's own storage root under the Creel.
  /// </summary>
  IHarnessStorage Storage { get; }

  /// <summary>
  ///   Gets the diagnostics factory.
  /// </summary>
  IDiagnostics Diagnostics { get; }

  /// <summary>
  ///   Gets the PSReadLine capability probe.
  /// </summary>
  IPSReadLineProbe PSReadLine { get; }

  /// <summary>
  ///   Gets the cross-harness directory.
  /// </summary>
  IHarnessDirectory Harnesses { get; }

  /// <summary>
  ///   Gets the read-only treadle catalog.
  /// </summary>
  ITreadleCatalog Treadles { get; }
}
