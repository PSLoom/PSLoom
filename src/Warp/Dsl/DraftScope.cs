// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   The root scope: the body of <c>Invoke-Loom -Draft</c>. Verbs such as <c>Style</c>, <c>Thread</c> and <c>Sley</c> live here.
/// </summary>
public sealed class DraftScope : DslScope {
  private DraftScope() { }
}
