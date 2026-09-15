// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Runtime.Hooks;
using PSLoom.TestKit;
using PSLoom.Warp.Hooks;

namespace PSLoom.Tests.Runtime.Hooks;

[TestSubject(typeof(HookBus))]
public sealed class HookBusTests {
  [Fact]
  public void Add_WithoutName_CreatesDistinctRegistrationsEachTime() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    var action = ScriptBlock.Create("1");

    var first = bus.Add(HookKind.DirectoryChanged, HookBus.ForScript(action), action, null);
    var second = bus.Add(HookKind.DirectoryChanged, HookBus.ForScript(action), action, null);

    first.Id.ShouldNotBe(second.Id);
    bus.Get(HookKind.DirectoryChanged).Count.ShouldBe(2);
  }

  [Fact]
  public void Add_WithName_ReplacesExistingRegistrationOfSameKindAndName() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);

    var first = AddScript(bus, HookKind.PrePrompt, "1", "my-hook");
    var second = AddScript(bus, HookKind.PrePrompt, "2", "MY-HOOK");

    var registration = bus.Get(HookKind.PrePrompt).ShouldHaveSingleItem();
    registration.Id.ShouldBe(second.Id);
    registration.Id.ShouldNotBe(first.Id);
  }

  [Fact]
  public void Add_WithName_DifferentKind_DoesNotReplace() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);

    AddScript(bus, HookKind.PrePrompt, "1", "my-hook");
    AddScript(bus, HookKind.PreExecute, "2", "my-hook");

    bus.Get().Count.ShouldBe(2);
  }

  [Fact]
  public void Remove_ById_RemovesOnlyThatRegistration() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    var first = AddScript(bus, HookKind.DirectoryChanged, "1");
    AddScript(bus, HookKind.DirectoryChanged, "2");

    bus.Remove(first.Id).ShouldBeTrue();
    bus.Get(HookKind.DirectoryChanged).Count.ShouldBe(1);
    bus.Remove(first.Id).ShouldBeFalse();
  }

  [Fact]
  public void Remove_ByKindAndName_RemovesMatchingRegistration() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    AddScript(bus, HookKind.CommandNotFound, "1", "named");

    bus.Remove(HookKind.CommandNotFound, "named").ShouldBeTrue();
    bus.Get(HookKind.CommandNotFound).ShouldBeEmpty();
    bus.Remove(HookKind.CommandNotFound, "named").ShouldBeFalse();
  }

  [Fact]
  public void Get_ReturnsRegistrationsInRegistrationOrder_AcrossKinds() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    var first = AddScript(bus, HookKind.PrePrompt, "1", "a");
    var second = AddScript(bus, HookKind.SessionExiting, "2", "b");
    var third = AddScript(bus, HookKind.PrePrompt, "3", "c");

    bus.Get().Select(registration => registration.Id).ShouldBe([first.Id, second.Id, third.Id]);
    bus.Get(name: "B").ShouldHaveSingleItem().Id.ShouldBe(second.Id);
  }

  [Fact]
  public void TryEnterDispatch_SecondCallForSameKind_ReturnsFalse_UntilExit() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);

    bus.TryEnterDispatch(HookKind.DirectoryChanged).ShouldBeTrue();
    bus.TryEnterDispatch(HookKind.DirectoryChanged).ShouldBeFalse();
    bus.TryEnterDispatch(HookKind.PrePrompt).ShouldBeTrue();

    bus.ExitDispatch(HookKind.DirectoryChanged);
    bus.TryEnterDispatch(HookKind.DirectoryChanged).ShouldBeTrue();
  }

  [Fact]
  public void Add_UnknownKind_ThrowsHookException() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);

    Should.Throw<HookException>(() => AddScript(bus, (HookKind)99, "1")).ErrorId.ShouldBe(HookException.UNKNOWN_KIND);
  }

  [Fact]
  public void Dispatch_NoRegistrations_DoesNotThrowOrAllocate() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    bus.Dispatch(HookKind.PrePrompt);

    var before = GC.GetAllocatedBytesForCurrentThread();

    for (var iteration = 0; iteration < 1_000; iteration++) {
      bus.Dispatch(HookKind.PrePrompt);
    }

    (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
  }

  [Fact]
  public void Dispatch_ThrowingHandler_DoesNotPreventSiblings_AndIsRecorded() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    var ran = false;
    var throwing = bus.Add(HookKind.PrePrompt, _ => throw new InvalidOperationException("boom"), null, "throwing");
    bus.Subscribe(HookKind.PrePrompt, _ => ran = true);

    Should.NotThrow(() => bus.Dispatch(HookKind.PrePrompt));

    ran.ShouldBeTrue();
    bus.Diagnostics.Snapshot().Last(entry => entry.RegistrationId == throwing.Id).Exception.ShouldNotBeNull();
  }

  [Fact]
  public void Dispatch_ScriptHandler_ThrowIsRecorded() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var defaultRunspace = PowerShellHost.UseAsDefault(runspace);
    var bus = new HookBus(runspace);
    var registration = AddScript(bus, HookKind.PrePrompt, "throw 'boom'");

    bus.Dispatch(HookKind.PrePrompt);

    bus.Diagnostics.Snapshot().Last(entry => entry.RegistrationId == registration.Id).Exception.ShouldNotBeNull();
  }

  [Fact]
  public void Dispatch_SlowHandler_IsFlaggedSlow() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace) { SlowThreshold = TimeSpan.Zero };
    var registration = bus.Add(HookKind.PrePrompt, _ => null, null, null);

    bus.Dispatch(HookKind.PrePrompt);

    bus.Diagnostics.Snapshot().Last(entry => entry.RegistrationId == registration.Id).Slow.ShouldBeTrue();
  }

  [Fact]
  public void Dispatch_AlreadyDispatchingSameKind_SkipsHandlers() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    var ran = false;
    bus.Subscribe(HookKind.PrePrompt, _ => ran = true);
    bus.TryEnterDispatch(HookKind.PrePrompt);

    bus.Dispatch(HookKind.PrePrompt);

    ran.ShouldBeFalse();
  }

  [Fact]
  public void Dispatch_HandlerReceivesTypedInvocationWithCapturedRunspace() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    HookInvocation? received = null;
    bus.Subscribe(HookKind.PreExecute, invocation => received = invocation);

    using (PowerShellHost.UseAsDefault(null)) {
      bus.DispatchPreExecute("Get-Process", 11);
    }

    var preExecute = received.ShouldBeOfType<PreExecuteInvocation>();
    preExecute.CommandLine.ShouldBe("Get-Process");
    preExecute.CursorPosition.ShouldBe(11);
    preExecute.Runspace.ShouldBeSameAs(runspace);
  }

  [Fact]
  public void Dispatch_ScriptHandler_ReceivesInvocationAsDollarUnderscoreAndFirstArgument() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var defaultRunspace = PowerShellHost.UseAsDefault(runspace);
    var bus = new HookBus(runspace);
    AddScript(bus, HookKind.PreExecute, "$global:fromPipeline = $_.CommandLine; $global:fromArgs = $args[0].CursorPosition");

    bus.DispatchPreExecute("Get-Process", 11);

    runspace.SessionStateProxy.GetVariable("fromPipeline").ShouldBe("Get-Process");
    runspace.SessionStateProxy.GetVariable("fromArgs").ShouldBe(11);
  }

  [Fact]
  public void SubscriptionHandle_Dispose_Unregisters() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    var calls = 0;
    var handle = bus.Subscribe(HookKind.Idle, _ => calls++);

    handle.Dispose();
    handle.Dispose();
    bus.Dispatch(HookKind.Idle);

    calls.ShouldBe(0);
    bus.Get().ShouldBeEmpty();
  }

  [Fact]
  public void RaiseSessionStarting_FiresOnce() {
    using var runspace = PowerShellHost.CreateRunspace();
    var bus = new HookBus(runspace);
    var calls = 0;
    bus.Subscribe(HookKind.SessionStarting, _ => calls++);

    bus.RaiseSessionStarting();
    bus.RaiseSessionStarting();

    calls.ShouldBe(1);
  }

  private static HookRegistration AddScript(HookBus bus, HookKind kind, string script, string? name = null) {
    var action = ScriptBlock.Create(script);
    return bus.Add(kind, HookBus.ForScript(action), action, name);
  }
}
