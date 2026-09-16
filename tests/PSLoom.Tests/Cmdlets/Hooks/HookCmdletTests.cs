// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Hooks;
using PSLoom.Runtime.Hooks;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Hooks;

namespace PSLoom.Tests.Cmdlets.Hooks;

[TestSubject(typeof(RegisterHookCmdlet))]
[TestSubject(typeof(UnregisterHookCmdlet))]
[TestSubject(typeof(GetHookCmdlet))]
[TestSubject(typeof(TraceHookCmdlet))]
public sealed class HookCmdletTests {
  [Fact]
  public void Register_ThenGet_ReturnsTheRegistration() {
    using var session = new KernelSession();

    session.Run("Register-Hook -Kind SessionExiting -Action { 1 } -Name 'my-hook' | Out-Null");
    var registration = Registrations(session, "Get-Hook").ShouldHaveSingleItem();

    session.Shell.HadErrors.ShouldBeFalse();
    registration.Kind.ShouldBe(HookKind.SessionExiting);
    registration.Name.ShouldBe("my-hook");
  }

  [Fact]
  public void Register_SameNameTwice_ReplacesPriorRegistration() {
    using var session = new KernelSession();

    session.Run("Register-Hook SessionExiting { 1 } -Name 'my-hook' | Out-Null");
    var second = Registrations(session, "Register-Hook SessionExiting { 2 } -Name 'my-hook'").ShouldHaveSingleItem();

    Registrations(session, "Get-Hook").ShouldHaveSingleItem().Id.ShouldBe(second.Id);
  }

  [Fact]
  public void GetHook_FilteredByKindAndName_ReturnsOnlyMatching() {
    using var session = new KernelSession();
    session.Run("Register-Hook SessionExiting { 1 } -Name 'a' | Out-Null; Register-Hook Idle { 2 } -Name 'b' | Out-Null");

    Registrations(session, "Get-Hook -Kind Idle").ShouldHaveSingleItem().Name.ShouldBe("b");
    Registrations(session, "Get-Hook -Name 'a'").ShouldHaveSingleItem().Kind.ShouldBe(HookKind.SessionExiting);
  }

  [Fact]
  public void Unregister_ById_RemovesRegistration() {
    using var session = new KernelSession();
    var registration = Registrations(session, "Register-Hook SessionExiting { 1 }").ShouldHaveSingleItem();

    session.Shell.AddCommand("Unregister-Hook").AddParameter("Id", registration.Id).Invoke();
    session.Shell.Commands.Clear();

    session.Shell.HadErrors.ShouldBeFalse();
    session.Run("Get-Hook").ShouldBeEmpty();
  }

  [Fact]
  public void Unregister_ByKindAndName_RemovesRegistration() {
    using var session = new KernelSession();
    session.Run("Register-Hook CommandNotFound { } -Name 'named' | Out-Null");

    session.Run("Unregister-Hook -Kind CommandNotFound -Name 'named'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Run("Get-Hook").ShouldBeEmpty();
  }

  [Fact]
  public void Unregister_UnknownId_WritesWarningAndDoesNotThrow() {
    using var session = new KernelSession();

    session.Run($"Unregister-Hook '{Guid.NewGuid()}'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Streams.Warning.ShouldNotBeEmpty();
  }

  [Fact]
  public void GetHook_PipedIntoUnregister_RemovesEveryMatchingRegistration() {
    using var session = new KernelSession();
    session.Run("Register-Hook SessionExiting { 1 } -Name 'a' | Out-Null; Register-Hook SessionExiting { 2 } -Name 'b' | Out-Null");

    session.Run("Get-Hook | Unregister-Hook");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Run("Get-Hook").ShouldBeEmpty();
  }

  [Fact]
  public void TraceHook_NothingRecordedYet_ReturnsEmpty() {
    // Harvested: TraceHookCmdletTests.Invoke_NothingRecordedYet_ReturnsEmpty.
    using var session = new KernelSession();

    session.Run("Trace-Hook").ShouldBeEmpty();
    session.Streams.Error.ShouldBeEmpty();
  }

  [Fact]
  public void TraceHook_ReturnsThrowingAndSlowEntries_WithLast() {
    using var session = new KernelSession();
    var bus = HookBus.PerRunspace.For(session.Runspace);
    bus.SlowThreshold = TimeSpan.Zero;
    var registration = Registrations(session, "Register-Hook SessionExiting { throw 'boom' } -Name 'log-throw'").ShouldHaveSingleItem();

    bus.Dispatch(HookKind.SessionExiting);
    bus.Dispatch(HookKind.SessionExiting);

    var entries = session.Run("Trace-Hook").Select(entry => entry.BaseObject).Cast<HookDiagnosticEntry>().ToArray();
    entries.ShouldContain(entry => entry.RegistrationId == registration.Id && entry.Exception != null && entry.Slow);
    session.Run("Trace-Hook -Last 1").Count.ShouldBe(1);
  }

  private static IReadOnlyList<HookRegistration> Registrations(KernelSession session, string script)
    => [.. session.Run(script).Select(item => item.BaseObject).Cast<HookRegistration>()];
}
