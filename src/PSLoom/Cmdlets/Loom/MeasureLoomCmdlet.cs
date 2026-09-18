// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Loom;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Loom;

/// <summary>
///   Reports the timings of the last draft woven in this session: prepass, harness imports, validation, each verb and the total.
/// </summary>
[Cmdlet(VerbsDiagnostic.Measure, "Loom")]
[OutputType(typeof(LoomTiming))]
public sealed class MeasureLoomCmdlet : PSCmdlet {
  /// <summary>
  ///   How verb timings are aggregated.
  /// </summary>
  public enum Grouping {
    /// <summary>Every row as recorded.</summary>
    None,

    /// <summary>Verb timings summed per owning harness.</summary>
    Harness,

    /// <summary>Verb timings summed per verb.</summary>
    Verb
  }

  /// <summary>
  ///   Gets or sets how verb timings are aggregated.
  /// </summary>
  [Parameter(Position = 0)]
  public Grouping GroupBy { get; set; } = Grouping.None;

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      if (LoomSession.PerRunspace.ForCurrent().LastRun is not { } timings) {
        WriteWarning("No draft has been woven in this session yet; run Invoke-Loom first.");
        return;
      }

      var all = timings.Concat(LoomSession.PerRunspace.ForCurrent().Sheds.Timings).ToArray();
      WriteObject(GroupBy == Grouping.None ? all : Group(all, GroupBy), true);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }

  private static IEnumerable<LoomTiming> Group(IReadOnlyList<LoomTiming> timings, Grouping grouping)
    => timings
      .Where(timing => timing.Phase == LoomPhase.Verb)
      .GroupBy(timing => grouping == Grouping.Harness ? timing.Harness ?? string.Empty : $"{timing.Harness}\\{timing.Name}",
        StringComparer.OrdinalIgnoreCase)
      .Select(group => {
        var first = group.First();

        return new LoomTiming(LoomPhase.Verb, grouping == Grouping.Harness ? first.Harness ?? string.Empty : first.Name, first.Harness, null, 0,
          TimeSpan.FromTicks(group.Sum(timing => timing.Inclusive.Ticks)), TimeSpan.FromTicks(group.Sum(timing => timing.Exclusive.Ticks)),
          group.Count());
      })
      .OrderByDescending(timing => timing.Exclusive);
}
