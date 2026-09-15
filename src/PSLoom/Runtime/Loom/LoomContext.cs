// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   The DSL as seen by a running verb or runner: the frame stack of its run and nested scope execution.
/// </summary>
internal sealed class LoomContext(LoomSession session, LoomRun run) : ILoomContext {
  /// <inheritdoc />
  public bool IsDraft => run.IsDraft;

  /// <inheritdoc />
  public void RunScoped<TScope>(IDslFrame<TScope> frame, ScriptBlock? body) where TScope : DslScope {
    ArgumentNullException.ThrowIfNull(frame);
    RunBody(frame, typeof(TScope), body);
  }

  /// <inheritdoc />
  public TFrame? Frame<TFrame>() where TFrame : class
    => run.FindFrame<TFrame>();

  /// <summary>
  ///   Pushes a frame, invokes a body with the scope's verbs, and pops. Failures of non-verb statements are reported into
  ///   the run; verbs report their own.
  /// </summary>
  internal void RunBody(object frame, Type scope, ScriptBlock? body) {
    var activeDepth = run.Active.Count;
    run.PushFrame(frame, scope);

    try {
      body?.InvokeWithContext(session.Verbs.TableFor(scope), []);
    }
    catch (PipelineStoppedException) {
      throw;
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      run.Report(LoomException.FromStatement(exception));
    }
    finally {
      run.PopFrame();

      while (run.Active.Count > activeDepth) {
        run.Active.Pop();
      }
    }
  }
}
