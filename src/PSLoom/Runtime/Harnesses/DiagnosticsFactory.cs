// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Diagnostics;
using PSLoom.Warp.Diagnostics;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   Creates the ring buffers harnesses back their <c>Trace-*</c> cmdlets with.
/// </summary>
internal sealed class DiagnosticsFactory : IDiagnostics {
  private DiagnosticsFactory() { }

  public static DiagnosticsFactory Instance { get; } = new();

  /// <inheritdoc />
  public IDiagnosticLog<TEntry> CreateLog<TEntry>(int capacity = RingBuffer<object>.DEFAULT_CAPACITY) where TEntry : class
    => new RingBuffer<TEntry>(capacity);
}
