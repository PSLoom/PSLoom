// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Diagnostics;

/// <summary>
///   Creates bounded, in-memory diagnostic logs backing a harness's <c>Trace-*</c> cmdlet.
/// </summary>
public interface IDiagnostics {
  /// <summary>
  ///   Creates a ring buffer that drops its oldest entry when full.
  /// </summary>
  /// <typeparam name="TEntry">The entry type.</typeparam>
  /// <param name="capacity">The maximum number of entries kept.</param>
  /// <returns>A log scoped to the harness and runspace.</returns>
  IDiagnosticLog<TEntry> CreateLog<TEntry>(int capacity = 200) where TEntry : class;
}
