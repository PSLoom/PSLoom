namespace PSLoom.Warp.Treadles;

/// <summary>
///   Describes a treadle change.
/// </summary>
public sealed class TreadleChangedEventArgs : EventArgs {
  internal TreadleChangedEventArgs(string name, TreadleDefinition? previous, TreadleDefinition? current) {
    Name = name;
    Previous = previous;
    Current = current;
  }

  /// <summary>
  ///   Gets the treadle name.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets the definition before the change; <see langword="null" /> on creation.
  /// </summary>
  public TreadleDefinition? Previous { get; }

  /// <summary>
  ///   Gets the definition after the change; <see langword="null" /> on removal.
  /// </summary>
  public TreadleDefinition? Current { get; }
}