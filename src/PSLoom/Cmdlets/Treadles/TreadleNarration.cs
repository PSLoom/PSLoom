// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Treadles;

namespace PSLoom.Cmdlets.Treadles;

/// <summary>
///   Reporting shared by the treadle cmdlets.
/// </summary>
internal static class TreadleNarration {
  /// <summary>
  ///   Writes one non-terminating error per subscriber that failed after write. The write itself already happened.
  /// </summary>
  public static void ReportSubscribers(PSCmdlet cmdlet, TreadleWriteOutcome outcome) {
    foreach (var failure in outcome.SubscriberFailures) {
      cmdlet.WriteError(TreadleException.SubscriberFailed(outcome.Name, failure).ToErrorRecord());
    }
  }
}
