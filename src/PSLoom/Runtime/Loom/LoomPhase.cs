// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Loom;

/// <summary>
///   The part of a loom run a timing belongs to.
/// </summary>
public enum LoomPhase {
  /// <summary>Finding <c>Thread</c> statements in the draft.</summary>
  Prepass,

  /// <summary>Importing one harness module.</summary>
  Import,

  /// <summary>Installing one missing harness module (first runs only).</summary>
  Install,

  /// <summary>Static scope validation of the draft.</summary>
  Validate,

  /// <summary>One verb invocation.</summary>
  Verb,

  /// <summary>Recording the statements a draft staged with <c>Shed</c>.</summary>
  Capture,

  /// <summary>A staged statement applied after the draft.</summary>
  Deferred,

  /// <summary>The whole run.</summary>
  Total
}
