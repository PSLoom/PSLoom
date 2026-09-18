// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Hooks;

namespace PSLoom.Runtime.Hooks;

/// <summary>
///   A registered hook handler.
/// </summary>
public sealed class HookRegistration {
  internal HookRegistration(HookKind kind, string? name, Func<HookInvocation, object?> invoke, ScriptBlock? action, long sequence,
  bool isInternal = false) {
    Id = Guid.NewGuid();
    Kind = kind;
    Name = name;
    Invoke = invoke;
    Action = action;
    Sequence = sequence;
    RegisteredAt = DateTimeOffset.UtcNow;
    IsInternal = isInternal;
  }

  /// <summary>
  ///   Gets the registration identifier.
  /// </summary>
  public Guid Id { get; }

  /// <summary>
  ///   Gets the event kind.
  /// </summary>
  public HookKind Kind { get; }

  /// <summary>
  ///   Gets the optional name; a later registration with the same kind and name replaces this one.
  /// </summary>
  public string? Name { get; }

  /// <summary>
  ///   Gets the script action, when registered through <c>Register-Hook</c>.
  /// </summary>
  public ScriptBlock? Action { get; }

  /// <summary>
  ///   Gets when the handler was registered.
  /// </summary>
  public DateTimeOffset RegisteredAt { get; }

  internal Func<HookInvocation, object?> Invoke { get; }

  internal long Sequence { get; }

  /// <summary>
  ///   Gets a value indicating whether the kernel registered this handler for itself; such handlers are not listed or removable.
  /// </summary>
  internal bool IsInternal { get; }
}
