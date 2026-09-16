// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   Where a statement staged with <c>Shed</c> stands.
/// </summary>
public enum ShedState {
  /// <summary>Captured, waiting in the queue.</summary>
  Pending,

  /// <summary>Waiting for its first use (on-demand).</summary>
  Armed,

  /// <summary>Applied without errors.</summary>
  Applied,

  /// <summary>Not applied because a condition was false.</summary>
  Skipped,

  /// <summary>Applied with errors, or a condition failed to evaluate.</summary>
  Failed,

  /// <summary>Replaced by a reweave before it applied.</summary>
  Superseded
}
