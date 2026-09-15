// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime;

namespace PSLoom.Cmdlets;

/// <summary>
///   Helpers shared by kernel cmdlets.
/// </summary>
internal static class CmdletExtensions {
  /// <summary>
  ///   Gets the engine intrinsics (<c>$ExecutionContext</c>) of the session running the cmdlet.
  /// </summary>
  /// <exception cref="KernelException">The intrinsics are unavailable (<c>LOOM_NO_ENGINE</c>).</exception>
  public static EngineIntrinsics GetEngine(this PSCmdlet cmdlet)
    => cmdlet.GetVariableValue("ExecutionContext") as EngineIntrinsics ?? throw KernelException.NoEngine();
}
