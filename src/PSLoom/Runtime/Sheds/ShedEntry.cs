// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   A statement the draft staged with <c>Shed</c>, and what became of it.
/// </summary>
public sealed class ShedEntry {
  private const int SUMMARY_LENGTH = 60;

  internal ShedEntry(ShedDeclaration declaration, string? file, string key, UInt128 fingerprint) {
    Declaration = declaration;
    File = file;
    Key = key;
    Fingerprint = fingerprint;
    Line = declaration.Statement.Extent.StartLineNumber;
    var firstLine = declaration.Statement.Extent.Text.Split('\n')[0].Trim();
    Statement = firstLine.Length <= SUMMARY_LENGTH ? firstLine : firstLine[..(SUMMARY_LENGTH - 1)] + "…";
  }

  /// <summary>Gets the draft line of the staged statement.</summary>
  public int Line { get; }

  /// <summary>Gets the first line of the statement, shortened.</summary>
  public string Statement { get; }

  /// <summary>Gets when the statement applies.</summary>
  public ShedTiming Timing => Declaration.Timing;

  /// <summary>Gets the slot; <c>0a</c> unless <c>-Slot</c> said otherwise.</summary>
  public string Slot => Declaration.Slot;

  /// <summary>Gets where the statement stands.</summary>
  public ShedState State { get; internal set; } = ShedState.Pending;

  /// <summary>Gets when the statement applied, was skipped or failed.</summary>
  public DateTimeOffset? CompletedAt { get; internal set; }

  /// <summary>Gets how long applying it took.</summary>
  public TimeSpan Elapsed { get; internal set; }

  /// <summary>Gets the errors recorded when it failed.</summary>
  public IReadOnlyList<ErrorRecord> Errors { get; internal set; } = [];

  internal ShedDeclaration Declaration { get; }

  internal string? File { get; }

  internal string Key { get; }

  internal UInt128 Fingerprint { get; }

  /// <summary>Gets the top-level verb invocations its apply recorded, for reweave.</summary>
  internal IReadOnlyList<LedgerItem> AppliedItems { get; set; } = [];

  /// <summary>Gets a value indicating whether the prompt warning already mentioned this entry.</summary>
  internal bool Warned { get; set; }

  // Apply-now bookkeeping, set by Admit and read by Applied.
  internal int ErrorsAtAdmit { get; set; }

  internal int LedgerAtAdmit { get; set; }

  internal long AdmittedAt { get; set; }

  internal void Complete(ShedState state, TimeSpan elapsed, IReadOnlyList<ErrorRecord> errors) {
    State = state;
    Elapsed = elapsed;
    Errors = errors;
    CompletedAt = DateTimeOffset.Now;
  }

  /// <inheritdoc />
  public override string ToString()
    => $"{Line}: {Statement} [{State}]";
}
