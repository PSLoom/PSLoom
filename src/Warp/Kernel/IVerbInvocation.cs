// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;

namespace PSLoom.Warp.Kernel;

/// <summary>
///   One running verb, as seen by the kernel.
/// </summary>
internal interface IVerbInvocation {
  /// <summary>
  ///   Gets the loom context exposed to the verb.
  /// </summary>
  ILoomContext Loom { get; }

  /// <summary>
  ///   Gets a value indicating whether <c>Weave</c> should run; <see langword="false" /> when a reweave found it unchanged.
  /// </summary>
  bool ShouldWeave { get; }

  /// <summary>
  ///   Records a failure on the run's error channel instead of the (possibly nested, possibly swallowing) pipeline.
  /// </summary>
  void ReportError(ErrorRecord errorRecord);

  /// <summary>
  ///   Ends the invocation: timing, reweave ledger entry.
  /// </summary>
  void Complete();
}
