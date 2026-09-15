// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Management.Automation.Language;
using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Verbs;
using PSLoom.Warp;
using PSLoom.Warp.Dsl;

namespace PSLoom.Cmdlets.Loom;

/// <summary>
///   Runs a draft once per session: loads the harnesses it threads, validates verb scopes, executes it with the loom
///   vocabulary, reports every verb failure, then raises <c>SessionStarting</c>.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "Loom")]
public sealed class InvokeLoomCmdlet : PSCmdlet {
  private const string HARNESS_MODULE_PREFIX = "PSLoom.";
  private const string MODULE_NOT_FOUND_ERROR_ID = "Modules_ModuleNotFound";
  private const string IMPORT_SCRIPT = "param($Name) Import-Module -Name $Name -Global -ErrorAction Stop";

  /// <summary>
  ///   Gets or sets the draft.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  public ScriptBlock Draft { get; set; } = null!;

  /// <summary>
  ///   Gets or sets a value indicating whether to load harnesses and validate the draft without executing it.
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
    var (harnesses, threadErrors) = DraftAnalyzer.FindThreads(draftAst);
    var problems = threadErrors.Select(error => error.ToErrorRecord()).ToList();
    AddPhase(run, LoomPhase.Prepass, nameof(LoomPhase.Prepass), null, started);

    foreach (var harness in harnesses) {
      started = Stopwatch.GetTimestamp();

      if (EnsureHarness(session, harness) is { } importError) {
        problems.Add(importError.ToErrorRecord());
      }

      AddPhase(run, LoomPhase.Import, harness, harness, started);
    }

    if (problems.Count > 0) {
      return problems;
    }

    started = Stopwatch.GetTimestamp();
    problems.AddRange(DraftValidator.Validate(draftAst, session.Verbs).Select(error => error.ToErrorRecord()));
    AddPhase(run, LoomPhase.Validate, nameof(LoomPhase.Validate), null, started);

    return problems;
  }

  private LoomException? EnsureHarness(LoomSession session, string harness) {
    if (session.Harnesses.TryGetByName(harness, out _)) {
      return null;
    }

    var moduleName = HARNESS_MODULE_PREFIX + harness;

    try {
      InvokeCommand.InvokeScript(IMPORT_SCRIPT, moduleName);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      return IsModuleNotFound(exception)
        ? LoomException.HarnessNotInstalled(harness, moduleName, exception)
        : LoomException.HarnessImportFailed(harness, moduleName, exception);
    }

    return session.Harnesses.TryGetByName(harness, out _) ? null : LoomException.NotAHarness(harness, moduleName);
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

  private static bool IsModuleNotFound(Exception exception) {
    for (var current = exception; current is not null; current = current.InnerException) {
      if (current is IContainsErrorRecord { ErrorRecord.FullyQualifiedErrorId: { } errorId } &&
          errorId.StartsWith(MODULE_NOT_FOUND_ERROR_ID, StringComparison.Ordinal)) {
        return true;
      }
    }

    return false;
  }

  private static void AddPhase(LoomRun run, LoomPhase phase, string name, string? harness, long started) {
    var elapsed = Stopwatch.GetElapsedTime(started);
    run.AddTiming(new LoomTiming(phase, name, harness, null, 0, elapsed, elapsed));
  }
}
