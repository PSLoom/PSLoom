namespace PSLoom.Warp.Diagnostics;

/// <summary>
///   A bounded diagnostic log. Writes never allocate beyond the entry itself.
/// </summary>
/// <typeparam name="TEntry">The entry type.</typeparam>
public interface IDiagnosticLog<TEntry> where TEntry : class {
  /// <summary>
  ///   Gets the maximum number of entries kept.
  /// </summary>
  int Capacity { get; }

  /// <summary>
  ///   Gets the number of entries currently kept.
  /// </summary>
  int Count { get; }

  /// <summary>
  ///   Appends an entry, dropping the oldest when full.
  /// </summary>
  /// <param name="entry">The entry.</param>
  void Record(TEntry entry);

  /// <summary>
  ///   Copies the most recent entries, oldest first.
  /// </summary>
  /// <param name="last">How many recent entries to return; all when <see langword="null" />.</param>
  /// <returns>A snapshot.</returns>
  IReadOnlyList<TEntry> Snapshot(int? last = null);

  /// <summary>
  ///   Removes every entry.
  /// </summary>
  void Clear();
}
