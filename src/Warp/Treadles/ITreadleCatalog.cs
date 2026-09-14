using System.Diagnostics.CodeAnalysis;

namespace PSLoom.Warp.Treadles;

/// <summary>
///   Read-only view of the session's treadles (parameterized aliases).
/// </summary>
public interface ITreadleCatalog {
  /// <summary>
  ///   Gets every treadle defined in the runspace.
  /// </summary>
  IReadOnlyCollection<TreadleDefinition> All { get; }

  /// <summary>
  ///   Raised after a treadle is created, replaced or removed.
  /// </summary>
  event EventHandler<TreadleChangedEventArgs>? Changed;

  /// <summary>
  ///   Looks up a treadle by name.
  /// </summary>
  /// <param name="name">The treadle name (case-insensitive, like PowerShell command names).</param>
  /// <param name="definition">The definition, when present.</param>
  /// <returns><see langword="true" /> when a treadle with that name exists.</returns>
  bool TryGet(string name, [NotNullWhen(true)] out TreadleDefinition? definition);
}