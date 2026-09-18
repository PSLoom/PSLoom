// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   One top-level verb invocation recorded by a loom run.
/// </summary>
public sealed class ReweaveEntry {
  internal ReweaveEntry(string verbName, string key, UInt128 fingerprint, IReadOnlyDictionary<string, object?> boundParameters) {
    VerbName = verbName;
    Key = key;
    Fingerprint = fingerprint;
    BoundParameters = boundParameters;
  }

  /// <summary>
  ///   Gets the DSL verb name.
  /// </summary>
  public string VerbName { get; }

  /// <summary>
  ///   Gets the identity key: the verb name plus the values of its <see cref="ReweaveKeyAttribute" /> parameters.
  /// </summary>
  public string Key { get; }

  /// <summary>
  ///   Gets the hash of the canonicalized bound parameters.
  /// </summary>
  public UInt128 Fingerprint { get; }

  /// <summary>
  ///   Gets the parameters the invocation was bound with.
  /// </summary>
  public IReadOnlyDictionary<string, object?> BoundParameters { get; }
}
