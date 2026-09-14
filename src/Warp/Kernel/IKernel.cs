// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hosting;

namespace PSLoom.Warp.Kernel;

/// <summary>
///   What the kernel implements and attaches through <see cref="HarnessHost.Attach" />. Internal: visible only to the
///   PSLoom kernel via <c>InternalsVisibleTo</c>, so it can change without breaking harnesses.
/// </summary>
internal interface IKernel {
  /// <summary>
  ///   Composes a harness for <c>Runspace.DefaultRunspace</c>.
  /// </summary>
  void RegisterHarness(HarnessRegistration registration);

  /// <summary>
  ///   Gets a composed harness's context for <c>Runspace.DefaultRunspace</c>.
  /// </summary>
  IHarnessContext GetContext(Type harnessType);

  /// <summary>
  ///   Starts one verb invocation: resolves the enclosing loom run, decides whether <c>Weave</c> runs (reweave), and
  ///   provides the error channel.
  /// </summary>
  IVerbInvocation BeginVerb(LoomVerb verb);
}
