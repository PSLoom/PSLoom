// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using JetBrains.Annotations;
using PSLoom.Runtime.Hooks;
using PSLoom.TestKit;
using PSLoom.Warp.Hooks;

namespace PSLoom.Tests;

[TestSubject(typeof(ModuleInitializer))]
public sealed class ModuleInitializerTests {
  [Fact]
  public void OnImport_SubscribesToEngineExitingEvent() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var defaultRunspace = PowerShellHost.UseAsDefault(runspace);

    new ModuleInitializer().OnImport();

    runspace.Events.GetEventSubscribers(ModuleInitializer.EXITING_SOURCE_IDENTIFIER).ShouldHaveSingleItem();
  }

  [Fact]
  public void OnImport_NoDefaultRunspace_DoesNotThrow() {
    using (PowerShellHost.UseAsDefault(null)) {
      Should.NotThrow(new ModuleInitializer().OnImport);
    }
  }

  [Fact]
  public void OnRemove_AfterOnImport_UnsubscribesFromEngineExiting() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var defaultRunspace = PowerShellHost.UseAsDefault(runspace);
    var initializer = new ModuleInitializer();
    initializer.OnImport();

    initializer.OnRemove(null!);

    runspace.Events.GetEventSubscribers(ModuleInitializer.EXITING_SOURCE_IDENTIFIER).ShouldBeEmpty();
  }

  [Fact]
  public void OnRemove_WithoutPriorOnImport_DoesNotThrow() => Should.NotThrow(() => new ModuleInitializer().OnRemove(null!));

  [Fact]
  public void OnEngineExiting_DispatchesSessionExitingHandlersOfCapturedRunspace() {
    using var runspace = PowerShellHost.CreateRunspace();
    var initializer = new ModuleInitializer();
    var ran = false;

    using (PowerShellHost.UseAsDefault(runspace)) {
      initializer.OnImport();
    }

    HookBus.PerRunspace.For(runspace).Subscribe(HookKind.SessionExiting, _ => ran = true);

    initializer.OnEngineExiting(null, null!);

    ran.ShouldBeTrue();
  }

  [Fact]
  public void EngineRaisingExiting_ReachesSubscription_AndScriptHandlersGetTheirRunspace() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var shell = PowerShellHost.CreateShell(runspace);
    var initializer = new ModuleInitializer();

    using (PowerShellHost.UseAsDefault(runspace)) {
      initializer.OnImport();
    }

    var action = ScriptBlock.Create("$global:exitRan = $true");
    HookBus.PerRunspace.For(runspace).Add(HookKind.SessionExiting, HookBus.ForScript(action), action, null);

    shell.Run("New-Event -SourceIdentifier ([System.Management.Automation.PSEngineEvent]::Exiting) | Out-Null");

    runspace.SessionStateProxy.GetVariable("exitRan").ShouldBe(true);
  }

  [Fact]
  public void OnEngineExiting_FromThreadWithoutDefaultRunspace_RunsScriptHandlers() {
    using var runspace = PowerShellHost.CreateRunspace();
    var initializer = new ModuleInitializer();

    using (PowerShellHost.UseAsDefault(runspace)) {
      initializer.OnImport();
    }

    var action = ScriptBlock.Create("$global:exitRan = $true");
    HookBus.PerRunspace.For(runspace).Add(HookKind.SessionExiting, HookBus.ForScript(action), action, null);

    using (PowerShellHost.UseAsDefault(null)) {
      Should.NotThrow(() => initializer.OnEngineExiting(null, null!));
      Runspace.DefaultRunspace.ShouldBeNull();
    }

    runspace.SessionStateProxy.GetVariable("exitRan").ShouldBe(true);
  }

  [Fact]
  public void OnEngineExiting_WithoutPriorOnImport_DoesNotThrow() => Should.NotThrow(() => new ModuleInitializer().OnEngineExiting(null, null!));
}
