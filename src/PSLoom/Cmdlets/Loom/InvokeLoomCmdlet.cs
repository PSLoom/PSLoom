// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Management.Automation.Language;
using PSLoom.Runtime.Harnesses;
using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Verbs;
using PSLoom.Warp;
using PSLoom.Warp.Dsl;

namespace PSLoom.Cmdlets.Loom;

/// <summary>
///   Runs a draft once per session: provides the first-party harnesses it threads (installing missing ones on a first run),
///   validates verb scopes, executes it with the loom vocabulary, reports every verb failure, then raises <c>SessionStarting</c>.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "Loom")]
public sealed class InvokeLoomCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the draft.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  public ScriptBlock Draft { get; set; } = null!;

  /// <summary>
  ///   Gets or sets a value indicating whether to load harnesses and validate the draft without executing or installing anything.
  /// </summary>
  [Parameter]
  public SwitchParameter Validate { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      Kernel.EnsureAttached();

      var session = LoomSession.PerRunspace.ForCurrent();
      session.AttachEngine(this.GetEngine());

      if (session.IsWoven &&
          !Validate) {
        WriteVerbose("The loom is already woven in this session; Invoke-Loom runs a draft once per session.");
        return;
      }

      var run = new LoomRun(true);
      var draftAst = (ScriptBlockAst)Draft.Ast;
      var problems = Prepare(session, run, draftAst);

      if (problems.Count > 0 ||
          Validate) {
        foreach (var problem in problems) {
          WriteError(problem);
        }

        if (problems.Count == 0) {
          WriteVerbose("The draft is valid.");
        }

        return;
      }

      Execute(session, run);
      run.AddTiming(new LoomTiming(LoomPhase.Total, nameof(LoomPhase.Total), null, null, 0, Stopwatch.GetElapsedTime(run.StartedAt),
        Stopwatch.GetElapsedTime(run.StartedAt)));
      session.MarkWoven(run);

      foreach (var error in run.Errors) {
        WriteError(error);
      }

      HookBus.PerRunspace.For(session.Runspace).RaiseSessionStarting();
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }

  private List<ErrorRecord> Prepare(LoomSession session, LoomRun run, ScriptBlockAst draftAst) {
    var started = Stopwatch.GetTimestamp();
    var (threads, threadErrors) = DraftAnalyzer.FindThreads(draftAst);
    var problems = threadErrors.Select(error => error.ToErrorRecord()).ToList();
    AddPhase(run, LoomPhase.Prepass, nameof(LoomPhase.Prepass), started);

    if (problems.Count > 0) {
      return problems;
    }

    var provisioner = new HarnessProvisioner(session, session.ModulesFor(this));

    foreach (var thread in threads) {
      if (provisioner.Provision(thread, !Validate, run) is { } error) {
        problems.Add(error.ToErrorRecord());
      }
    }

    if (problems.Count > 0) {
      return problems;
    }

    started = Stopwatch.GetTimestamp();
    problems.AddRange(DraftValidator.Validate(draftAst, session.Verbs).Select(error => error.ToErrorRecord()));
    AddPhase(run, LoomPhase.Validate, nameof(LoomPhase.Validate), started);

    return problems;
  }

  private void Execute(LoomSession session, LoomRun run) {
    session.PushRun(run);
    run.PushFrame(new DraftFrame(), typeof(DraftScope));

    try {
      foreach (var output in Draft.InvokeWithContext(session.Verbs.TableFor(typeof(DraftScope)), [])) {
        WriteObject(output);
      }
    }
    catch (PipelineStoppedException) {
      throw;
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      run.Report(LoomException.FromStatement(exception));
    }
    finally {
      run.PopFrame();
      run.Active.Clear();
      session.PopRun(run);
    }
  }

  private static void AddPhase(LoomRun run, LoomPhase phase, string name, long started) {
    var elapsed = Stopwatch.GetElapsedTime(started);
    run.AddTiming(new LoomTiming(phase, name, null, null, 0, elapsed, elapsed));
  }
}
