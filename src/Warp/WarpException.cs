// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp;

/// <summary>
///   Raised by the contract itself: an unusable harness definition, an incompatible contract version, or a missing kernel.
/// </summary>
public sealed class WarpException : PowerShellException {
  internal const string CONTRACT_MISMATCH = "WARP_CONTRACT_MISMATCH";
  internal const string HARNESS_ATTRIBUTE_MISSING = "WARP_HARNESS_ATTRIBUTE_MISSING";
  internal const string KERNEL_UNAVAILABLE = "WARP_KERNEL_UNAVAILABLE";
  internal const string KERNEL_ALREADY_ATTACHED = "WARP_KERNEL_ALREADY_ATTACHED";
  internal const string FRAME_MISSING = "WARP_FRAME_MISSING";
  internal const string VERB_OUTSIDE_LOOM = "WARP_VERB_OUTSIDE_LOOM";
  internal const string VERB_UNHANDLED_EXCEPTION = "WARP_VERB_UNHANDLED_EXCEPTION";
  internal const string SCOPE_INVALID = "WARP_SCOPE_INVALID";

  private WarpException(string errorId, ErrorCategory errorCategory, string message, object? targetObject)
    : base(errorId, errorCategory, message, targetObject) { }

  internal static WarpException ContractMismatch(string harnessAssembly, Version? compiled, Version loaded)
    => new(
      CONTRACT_MISMATCH,
      ErrorCategory.InvalidOperation,
      compiled is null
        ? $"'{harnessAssembly}' does not reference Warp; it cannot be loaded as a harness."
        : $"'{harnessAssembly}' targets Warp {compiled.Major}.x, but Warp {loaded.Major}.x is loaded. Update the harness or PSLoom.",
      harnessAssembly);

  internal static WarpException HarnessAttributeMissing(Type harnessType)
    => new(
      HARNESS_ATTRIBUTE_MISSING,
      ErrorCategory.InvalidData,
      $"'{harnessType.FullName}' implements IHarness but is not marked with [Harness(\"<name>\")].",
      harnessType);

  internal static WarpException KernelUnavailable()
    => new(
      KERNEL_UNAVAILABLE,
      ErrorCategory.ResourceUnavailable,
      "The PSLoom kernel is not loaded. Import the PSLoom module before using a harness.",
      null);

  internal static WarpException KernelAlreadyAttached()
    => new(
      KERNEL_ALREADY_ATTACHED,
      ErrorCategory.InvalidOperation,
      "A different PSLoom kernel is already attached to this process.",
      null);

  internal static WarpException FrameMissing(Type frameType)
    => new(
      FRAME_MISSING,
      ErrorCategory.InvalidOperation,
      $"No enclosing '{frameType.Name}' frame. This verb must be used inside the scope that provides it.",
      frameType);

  internal static WarpException VerbOutsideLoom(string verbName)
    => new(
      VERB_OUTSIDE_LOOM,
      ErrorCategory.InvalidOperation,
      $"'{verbName}' is a loom verb and can only run inside Invoke-Loom or a harness DSL block.",
      verbName);

  internal static WarpException ScopeInvalid(Type scopeType, Type owner, string reason)
    => new(
      SCOPE_INVALID,
      ErrorCategory.InvalidData,
      $"'{scopeType.FullName}' (referenced by '{owner.FullName}') is not a valid DSL scope: {reason}",
      scopeType);

  internal static ErrorRecord VerbUnhandled(Exception exception, string verbName)
    => new(exception, VERB_UNHANDLED_EXCEPTION, ErrorCategory.NotSpecified, verbName);
}
