// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   What a running verb sees of the DSL: the frame stack and the ability to open a nested scope.
/// </summary>
public interface ILoomContext {
  /// <summary>
  ///   Gets a value indicating whether the verb runs inside <c>Invoke-Loom</c> (as opposed to a harness cmdlet running a DSL
  ///   body through <see cref="IDslRunner" />).
  /// </summary>
  bool IsDraft { get; }

  /// <summary>
  ///   Pushes <paramref name="frame" />, invokes <paramref name="body" /> with the verb table of <typeparamref name="TScope" />,
  ///   and pops. Failures inside the body go to the run's error channel; this method returns normally after them.
  /// </summary>
  /// <typeparam name="TScope">The scope to open, inferred from the frame.</typeparam>
  /// <param name="frame">The state for the new scope.</param>
  /// <param name="body">The body to invoke; <see langword="null" /> pushes and pops without invoking anything.</param>
  void RunScoped<TScope>(IDslFrame<TScope> frame, ScriptBlock? body) where TScope : DslScope;

  /// <summary>
  ///   Finds the nearest enclosing frame of a type.
  /// </summary>
  /// <typeparam name="TFrame">The frame type.</typeparam>
  /// <returns>The nearest frame, or <see langword="null" /> when no enclosing scope has one.</returns>
  TFrame? Frame<TFrame>() where TFrame : class;

  /// <summary>
  ///   Finds the nearest enclosing frame of a type, or fails.
  /// </summary>
  /// <typeparam name="TFrame">The frame type.</typeparam>
  /// <returns>The nearest frame.</returns>
  /// <exception cref="WarpException">No enclosing frame of that type (<c>WARP_FRAME_MISSING</c>).</exception>
  TFrame RequireFrame<TFrame>() where TFrame : class
    => Frame<TFrame>() ?? throw WarpException.FrameMissing(typeof(TFrame));
}
