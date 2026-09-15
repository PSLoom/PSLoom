// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Hooks;
using PSLoom.TestKit;
using PSLoom.Warp.Hooks;

namespace PSLoom.Tests.Runtime.Hooks;

[TestSubject(typeof(HookBridge))]
public sealed class HookBridgeTests {
  [Fact]
  public void PrePrompt_NoDefaultRunspace_DoesNotThrow() {
    using (PowerShellHost.UseAsDefault(null)) {
      Should.NotThrow(HookBridge.PrePrompt);
    }
  }

  [Fact]
  public void PrePrompt_RunspaceWithoutBus_DoesNotCreateOne() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var defaultRunspace = PowerShellHost.UseAsDefault(runspace);

    HookBridge.PrePrompt();

    HookBus.PerRunspace.TryGet(runspace, out var _).ShouldBeFalse();
  }

  [Fact]
  public void PrePrompt_RaisesSessionStartingOnceThenPrePromptEachTime() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var defaultRunspace = PowerShellHost.UseAsDefault(runspace);
    var bus = HookBus.PerRunspace.For(runspace);
    var events = new List<HookKind>();
    bus.Subscribe(HookKind.SessionStarting, invocation => events.Add(invocation.Kind));
    bus.Subscribe(HookKind.PrePrompt, invocation => events.Add(invocation.Kind));

    HookBridge.PrePrompt();
    HookBridge.PrePrompt();

    events.ShouldBe([HookKind.SessionStarting, HookKind.PrePrompt, HookKind.PrePrompt]);
  }

  [Fact]
  public void PrePrompt_HandlerThrows_DoesNotPropagate() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var defaultRunspace = PowerShellHost.UseAsDefault(runspace);
    HookBus.PerRunspace.For(runspace).Subscribe(HookKind.PrePrompt, _ => throw new InvalidOperationException("boom"));

    Should.NotThrow(HookBridge.PrePrompt);
  }
}
