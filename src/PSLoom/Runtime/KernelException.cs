// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp;

namespace PSLoom.Runtime;

/// <summary>
///   Raised by kernel plumbing shared across subsystems.
/// </summary>
public sealed class KernelException : PowerShellException {
  internal const string NO_RUNSPACE = "LOOM_NO_RUNSPACE";
  internal const string NO_ENGINE = "LOOM_NO_ENGINE";

  private KernelException(string errorId, ErrorCategory errorCategory, string message)
    : base(errorId, errorCategory, message) { }

  internal static KernelException NoRunspace()
    => new(NO_RUNSPACE, ErrorCategory.InvalidOperation, "There is no current runspace. PSLoom state is per runspace.");

  internal static KernelException NoEngine()
    => new(NO_ENGINE, ErrorCategory.InvalidOperation, "The PowerShell engine intrinsics are not available in this context.");
}
