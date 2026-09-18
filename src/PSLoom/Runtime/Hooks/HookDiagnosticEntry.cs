// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Hooks;

namespace PSLoom.Runtime.Hooks;

/// <summary>
///   One recorded hook invocation.
/// </summary>
public sealed class HookDiagnosticEntry {
  internal HookDiagnosticEntry(HookRegistration registration, TimeSpan elapsed, bool slow, Exception? exception) {
    Kind = registration.Kind;
    RegistrationId = registration.Id;
    Name = registration.Name;
    Timestamp = DateTimeOffset.UtcNow;
    Elapsed = elapsed;
    Slow = slow;
    Exception = exception;
  }

  /// <summary>
  ///   Gets the event kind.
  /// </summary>
  public HookKind Kind { get; }

  /// <summary>
  ///   Gets the registration identifier.
  /// </summary>
  public Guid RegistrationId { get; }

  /// <summary>
  ///   Gets the registration name, if any.
  /// </summary>
  public string? Name { get; }

  /// <summary>
  ///   Gets when the handler finished.
  /// </summary>
  public DateTimeOffset Timestamp { get; }

  /// <summary>
  ///   Gets how long the handler ran.
  /// </summary>
  public TimeSpan Elapsed { get; }

  /// <summary>
  ///   Gets a value indicating whether the handler exceeded the slow threshold. Diagnostic only: a running handler is never preempted.
  /// </summary>
  public bool Slow { get; }

  /// <summary>
  ///   Gets the exception the handler threw, if any.
  /// </summary>
  public Exception? Exception { get; }
}
