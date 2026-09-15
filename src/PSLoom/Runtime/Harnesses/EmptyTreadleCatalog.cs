// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using PSLoom.Warp.Treadles;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   The treadle catalog until treadles exist: always empty, never changes.
/// </summary>
internal sealed class EmptyTreadleCatalog : ITreadleCatalog {
  private EmptyTreadleCatalog() { }

  public static EmptyTreadleCatalog Instance { get; } = new();

  /// <inheritdoc />
  public IReadOnlyCollection<TreadleDefinition> All => [];

  /// <inheritdoc />
  public event EventHandler<TreadleChangedEventArgs>? Changed {
    add { }
    remove { }
  }

  /// <inheritdoc />
  public bool TryGet(string name, [NotNullWhen(true)] out TreadleDefinition? definition) {
    definition = null;
    return false;
  }
}
