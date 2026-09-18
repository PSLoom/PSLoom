// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   Runs a DSL body outside <c>Invoke-Loom</c>, e.g. from <c>New-Completer -ScriptBlock</c>.
/// </summary>
public interface IDslRunner {
  /// <summary>
  ///   Invokes <paramref name="body" /> as the root of <typeparamref name="TScope" /> and collects every failure.
  /// </summary>
  /// <typeparam name="TScope">The scope, inferred from the frame.</typeparam>
  /// <param name="frame">The root frame.</param>
  /// <param name="body">The body.</param>
  /// <returns>Every failure raised inside the body, in order; empty on success. The caller writes them.</returns>
  IReadOnlyList<ErrorRecord> Run<TScope>(IDslFrame<TScope> frame, ScriptBlock body) where TScope : DslScope;
}
