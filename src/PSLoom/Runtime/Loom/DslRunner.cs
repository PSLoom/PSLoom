// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   Runs a harness DSL body outside <c>Invoke-Loom</c> with the same scope machinery, collecting its errors.
/// </summary>
internal sealed class DslRunner(LoomSession session) : IDslRunner {
  /// <inheritdoc />
  public IReadOnlyList<ErrorRecord> Run<TScope>(IDslFrame<TScope> frame, ScriptBlock body) where TScope : DslScope {
    ArgumentNullException.ThrowIfNull(frame);
    ArgumentNullException.ThrowIfNull(body);

    var run = new LoomRun(false);
    session.PushRun(run);

    try {
      new LoomContext(session, run).RunBody(frame, typeof(TScope), body);
    }
    finally {
      session.PopRun(run);
    }

    return run.Errors;
  }
}
