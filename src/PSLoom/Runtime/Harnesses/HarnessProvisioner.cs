// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Verbs;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   Makes a threaded harness available: first-party check, import first, install only when missing (under the install lock),
///   never update.
/// </summary>
internal sealed class HarnessProvisioner(LoomSession session, IHarnessModules modules) {
  /// <summary>
  ///   Ensures the harness is composed in the session.
  /// </summary>
  /// <param name="thread">The declaration from the draft.</param>
  /// <param name="allowInstall"><see langword="false" /> for <c>-Validate</c>: a missing module is reported, never installed.</param>
  /// <param name="run">Receives import and install timings.</param>
  /// <returns>The error, or <see langword="null" /> when the harness is ready.</returns>
  public LoomException? Provision(ThreadDeclaration thread, bool allowInstall, LoomRun run) {
    var name = thread.Name;

    if (!session.FirstParty.Contains(name)) {
      return LoomException.HarnessNotFirstParty(name, session.FirstParty);
    }

    if (session.Harnesses.TryGetByName(name, out var composed)) {
      return VersionError(thread, composed);
    }

    var moduleName = FirstPartyHarnesses.ModuleName(name);
    var started = Stopwatch.GetTimestamp();
    var import = modules.Import(moduleName, thread.Version);
    AddTiming(run, LoomPhase.Import, name, started);

    switch (import.Status) {
      case ModuleImportStatus.Imported:
        return ComposedError(thread, moduleName);
      case ModuleImportStatus.Failed:
        return LoomException.HarnessImportFailed(name, moduleName, import.Exception!);
    }

    if (!allowInstall) {
      return LoomException.HarnessNotInstalled(name, moduleName, thread.Version, import.Exception);
    }

    started = Stopwatch.GetTimestamp();

    try {
      return Install(thread, moduleName);
    }
    finally {
      AddTiming(run, LoomPhase.Install, name, started);
    }
  }

  private LoomException? Install(ThreadDeclaration thread, string moduleName) {
    modules.WriteProgress($"Installing {moduleName}{(thread.Version is null ? string.Empty : $" {thread.Version}")}…");

    IDisposable held;

    try {
      held = session.InstallLock.Acquire(modules.WriteProgress);
    }
    catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException) {
      return LoomException.HarnessInstallFailed(thread.Name, moduleName, thread.Version, exception.Message, exception);
    }

    using (held) {
      // Another session may have installed it while this one waited for the lock.
      var import = modules.Import(moduleName, thread.Version);

      if (import.Status == ModuleImportStatus.NotFound) {
        try {
          modules.Install(moduleName, thread.Version);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
          return LoomException.HarnessInstallFailed(thread.Name, moduleName, thread.Version, Reason(exception), exception);
        }

        import = modules.Import(moduleName, thread.Version);
      }

      return import.Status switch {
        ModuleImportStatus.Imported => ComposedError(thread, moduleName),
        ModuleImportStatus.NotFound => LoomException.HarnessInstallFailed(thread.Name, moduleName, thread.Version,
          "it was installed but could not be found afterwards.", import.Exception),
        var _ => LoomException.HarnessImportFailed(thread.Name, moduleName, import.Exception!)
      };
    }
  }

  private LoomException? ComposedError(ThreadDeclaration thread, string moduleName)
    => session.Harnesses.TryGetByName(thread.Name, out var composed)
      ? VersionError(thread, composed)
      : LoomException.NotAHarness(thread.Name, moduleName);

  private static LoomException? VersionError(ThreadDeclaration thread, HarnessServices composed)
    => thread.Version is { } requested && !VersionsMatch(requested, composed.Identity.ModuleVersion)
      ? LoomException.HarnessVersionConflict(thread.Name, requested, composed.Identity.ModuleVersion)
      : null;

  /// <summary>
  ///   Compares versions treating missing build and revision components as zero (1.2 equals 1.2.0.0).
  /// </summary>
  internal static bool VersionsMatch(Version requested, Version actual)
    => requested.Major == actual.Major &&
       requested.Minor == actual.Minor &&
       Math.Max(requested.Build, 0) == Math.Max(actual.Build, 0) &&
       Math.Max(requested.Revision, 0) == Math.Max(actual.Revision, 0);

  /// <summary>
  ///   Gets the message of the error that actually failed, not PowerShell's wrappers ("The running command stopped because the
  ///   preference variable…", method or cmdlet invocation exceptions).
  /// </summary>
  internal static string Reason(Exception exception) {
    var root = exception;

    while (true) {
      if (root is ActionPreferenceStopException { ErrorRecord.Exception: { } recorded } &&
          !ReferenceEquals(recorded, root)) {
        root = recorded;
      }
      else if (root is (CmdletInvocationException or MethodInvocationException) and { InnerException: { } inner }) {
        root = inner;
      }
      else {
        break;
      }
    }

    var message = root.Message.Trim();
    return message.EndsWith('.') ? message : message + ".";
  }

  private static void AddTiming(LoomRun run, LoomPhase phase, string harness, long started) {
    var elapsed = Stopwatch.GetElapsedTime(started);
    run.AddTiming(new LoomTiming(phase, harness, harness, null, 0, elapsed, elapsed));
  }
}
