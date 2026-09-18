// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Hosting;
using PSLoom.Warp.Kernel;

namespace PSLoom.Warp.Dsl;

/// <summary>
///   Base class of every DSL verb. Parameters are ordinary <see cref="ParameterAttribute" /> properties bound by PowerShell;
///   the work goes in <see cref="Weave" />. The cmdlet lifecycle is sealed so every failure reaches the loom run's error channel
///   exactly once, even when the verb executes inside a nested pipeline that would otherwise swallow it.
/// </summary>
public abstract class LoomVerb : PSCmdlet {
  private IVerbInvocation? _invocation;

  /// <summary>
  ///   Gets the loom context of the current invocation.
  /// </summary>
  /// <exception cref="InvalidOperationException">Accessed outside <see cref="Weave" />.</exception>
  protected ILoomContext Loom
    => _invocation?.Loom ?? throw new InvalidOperationException("Loom is only available while the verb is weaving.");

  /// <summary>
  ///   Performs the verb. Throw a <see cref="PowerShellException" /> or call <see cref="ReportError(ErrorRecord)" /> to fail;
  ///   the draft continues either way.
  /// </summary>
  protected abstract void Weave();

  /// <summary>
  ///   Reports a non-terminating failure on the run's error channel.
  /// </summary>
  /// <param name="errorRecord">The error.</param>
  protected void ReportError(ErrorRecord errorRecord) {
    ArgumentNullException.ThrowIfNull(errorRecord);

    if (_invocation is { } invocation) {
      invocation.ReportError(errorRecord);
      return;
    }

    WriteError(errorRecord);
  }

  /// <summary>
  ///   Reports a non-terminating failure on the run's error channel.
  /// </summary>
  /// <param name="exception">The error.</param>
  protected void ReportError(PowerShellException exception) {
    ArgumentNullException.ThrowIfNull(exception);
    ReportError(exception.ToErrorRecord());
  }

  /// <inheritdoc />
  protected sealed override void BeginProcessing() {
    if (!HarnessHost.IsAttached) {
      ThrowTerminatingError(WarpException.VerbOutsideLoom(MyInvocation.MyCommand?.Name ?? GetType().Name).ToErrorRecord());
    }

    try {
      _invocation = HarnessHost.Kernel.BeginVerb(this);
    }
    catch (PowerShellException exception) {
      ThrowTerminatingError(exception.ToErrorRecord());
    }
  }

  /// <inheritdoc />
  protected sealed override void ProcessRecord() {
    if (_invocation is not { ShouldWeave: true } invocation) {
      return;
    }

    try {
      Weave();
    }
    catch (PipelineStoppedException) {
      throw;
    }
    catch (PowerShellException exception) {
      invocation.ReportError(exception.ToErrorRecord());
    }
    catch (RuntimeException exception) {
      invocation.ReportError(exception.ErrorRecord);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      invocation.ReportError(WarpException.VerbUnhandled(exception, MyInvocation.MyCommand?.Name ?? GetType().Name));
    }
  }

  /// <inheritdoc />
  protected sealed override void EndProcessing()
    => _invocation?.Complete();
}
