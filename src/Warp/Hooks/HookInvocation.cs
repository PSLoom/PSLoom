// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;

namespace PSLoom.Warp.Hooks;

/// <summary>
///   One hook event. Kinds with a payload use a derived type.
/// </summary>
public class HookInvocation {
  internal HookInvocation(HookKind kind, Runspace runspace) {
    Kind = kind;
    Runspace = runspace;
  }

  /// <summary>
  ///   Gets the event kind.
  /// </summary>
  public HookKind Kind { get; }

  /// <summary>
  ///   Gets the runspace the event belongs to, captured at subscription; do not read <c>Runspace.DefaultRunspace</c> instead.
  /// </summary>
  public Runspace Runspace { get; }
}
