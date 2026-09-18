// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;

namespace PSLoom.Runtime;

/// <summary>
///   Per-runspace state. A value lives exactly as long as its runspace: nothing leaks across runspaces and nothing needs cleanup.
/// </summary>
/// <typeparam name="T">The state type.</typeparam>
internal sealed class RunspaceLocal<T> where T : class {
  private readonly ConditionalWeakTable<Runspace, T>.CreateValueCallback _create;
  private readonly ConditionalWeakTable<Runspace, T> _values = new();

  /// <summary>
  ///   Initializes a new instance of the <see cref="RunspaceLocal{T}" /> class.
  /// </summary>
  /// <param name="factory">Creates the value for a runspace on first access.</param>
  public RunspaceLocal(Func<Runspace, T> factory) {
    ArgumentNullException.ThrowIfNull(factory);
    _create = runspace => factory(runspace);
  }

  /// <summary>
  ///   Gets the value for a runspace, creating it on first access.
  /// </summary>
  public T For(Runspace runspace) {
    ArgumentNullException.ThrowIfNull(runspace);
    return _values.GetValue(runspace, _create);
  }

  /// <summary>
  ///   Gets the value for <see cref="Runspace.DefaultRunspace" />. Reliable only on the thread the runspace is executing on.
  /// </summary>
  /// <exception cref="KernelException">There is no current runspace (<c>LOOM_NO_RUNSPACE</c>).</exception>
  public T ForCurrent()
    => For(Runspace.DefaultRunspace ?? throw KernelException.NoRunspace());

  /// <summary>
  ///   Gets the value for a runspace without creating it.
  /// </summary>
  public bool TryGet(Runspace runspace, [NotNullWhen(true)] out T? value)
    => _values.TryGetValue(runspace, out value);
}
