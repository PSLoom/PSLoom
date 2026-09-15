// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Runtime.Hooks;
using PSLoom.TestKit;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Hooks;

namespace PSLoom.Tests.Cmdlets.Hooks;

[TestSubject(typeof(HookWiring))]
public sealed class HookWiringTests {
  [Fact]
  public void DirectoryChanged_RegisteredHook_RunsOnSetLocation_WithTypedInvocation() {
    using var session = new KernelSession();

    session.Run("Register-Hook DirectoryChanged { $global:newPath = $_.EventArgs.NewPath.Path } | Out-Null");
    session.Run($"Set-Location -Path '{TempPathLiteral()}'");

    session.Shell.HadErrors.ShouldBeFalse();
    ((string)session.Global("newPath")!).TrimEnd('\\', '/').ShouldBe(Path.GetTempPath().TrimEnd('\\', '/'));
  }

  [Fact]
  public void DirectoryChanged_PreExistingLocationChangedAction_StillFires() {
    using var session = new KernelSession();
    session.Run("$ExecutionContext.SessionState.InvokeCommand.LocationChangedAction = { $global:preExistingRan = $true }");

    session.Run("Register-Hook DirectoryChanged { $global:chpwdRan = $true } | Out-Null");
    session.Run($"Set-Location -Path '{TempPathLiteral()}'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Global("preExistingRan").ShouldBe(true);
    session.Global("chpwdRan").ShouldBe(true);
  }

  [Fact]
  public void DirectoryChanged_RegisteredTwice_WiresOnce() {
    using var session = new KernelSession();

    session.Run("Register-Hook DirectoryChanged { $global:chpwdCount = ([int]$global:chpwdCount) + 1 } | Out-Null");
    session.Run("Register-Hook DirectoryChanged { } | Out-Null");
    session.Run($"Set-Location -Path '{TempPathLiteral()}'");

    session.Global("chpwdCount").ShouldBe(1);
  }

  [Fact]
  public void CommandNotFound_HookReturningScriptBlock_SubstitutesIt() {
    using var session = new KernelSession();
    session.Run("Register-Hook CommandNotFound { { 'substituted-output' } } | Out-Null");

    var result = session.Run("DoesNotExist12345");

    session.Shell.HadErrors.ShouldBeFalse();
    result.ShouldHaveSingleItem().ToString().ShouldBe("substituted-output");
  }

  [Fact]
  public void CommandNotFound_HookSettingEventArgs_SubstitutesAndSkipsLaterHooks() {
    using var session = new KernelSession();
    session.Run("Register-Hook CommandNotFound { $_.EventArgs.CommandScriptBlock = { \"ran $($args.Count)\" } } | Out-Null");
    session.Run("Register-Hook CommandNotFound { $global:secondRan = $true } | Out-Null");

    var result = session.Run("DoesNotExist12345");

    session.Shell.HadErrors.ShouldBeFalse();
    result.ShouldHaveSingleItem().ToString().ShouldStartWith("ran");
    session.Global("secondRan").ShouldBeNull();
  }

  [Fact]
  public void CommandNotFound_WithoutResolvingHook_StillProducesCommandNotFoundError() {
    using var session = new KernelSession();
    session.Run("Register-Hook CommandNotFound { $global:observed = $_.CommandName } | Out-Null");

    session.Run("DoesNotExist12345");

    session.Shell.HadErrors.ShouldBeTrue();
    session.Streams.Error.ShouldContain(error => error.CategoryInfo.Category == ErrorCategory.ObjectNotFound);
    session.Global("observed").ShouldBe("DoesNotExist12345");
  }

  [Fact]
  public void PrePrompt_RunsHookAndPreservesOriginalOutput() {
    using var session = new KernelSession();
    session.Run("function global:prompt { 'MY-PROMPT> ' }");

    session.Run("Register-Hook PrePrompt { $global:precmdRan = $true } | Out-Null");
    var result = session.Run("prompt");

    session.Shell.HadErrors.ShouldBeFalse();
    result.ShouldHaveSingleItem().ToString().ShouldBe("MY-PROMPT> ");
    session.Global("precmdRan").ShouldBe(true);
  }

  [Fact]
  public void PrePrompt_RegisteredTwice_WrapsOnce() {
    using var session = new KernelSession();
    session.Run("function global:prompt { 'ORIGINAL> ' }");

    session.Run("Register-Hook PrePrompt { $global:precmdCount = ([int]$global:precmdCount) + 1 } | Out-Null");
    session.Run("Register-Hook PrePrompt { } | Out-Null");
    session.Run("prompt");

    session.Global("precmdCount").ShouldBe(1);
  }

  [Fact]
  public void PrePrompt_FirstPrompt_RaisesSessionStartingOnce() {
    using var session = new KernelSession();
    session.Run("Register-Hook SessionStarting { $global:startCount = ([int]$global:startCount) + 1 } | Out-Null");

    session.Run("prompt | Out-Null; prompt | Out-Null");

    session.Global("startCount").ShouldBe(1);
  }

  [Fact]
  public void PrePrompt_PromptReplacedAfterWrapping_IsRewrappedAtNextCheck() {
    using var session = new KernelSession();
    session.Run("Register-Hook PrePrompt { $global:precmdCount = ([int]$global:precmdCount) + 1 } | Out-Null");
    session.Run("function global:prompt { 'TOOL> ' }");

    using (PowerShellHost.UseAsDefault(session.Runspace)) {
      HookBus.PerRunspace.For(session.Runspace).Wiring.EnsurePromptCurrent();
    }

    session.Run("prompt").ShouldHaveSingleItem().ToString().ShouldBe("TOOL> ");
    session.Global("precmdCount").ShouldBe(1);
  }

  [Fact]
  public void PreExecute_WithoutPSReadLine_WritesWarningAndIsRetriedLater() {
    using var session = new KernelSession();
    // Keep an installed PSReadLine from being auto-imported, so the session truly has none.
    session.Run("$global:PSModuleAutoLoadingPreference = 'None'");

    session.Run("Register-Hook PreExecute { } | Out-Null");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Streams.Warning.ShouldNotBeEmpty();
    HookBus.PerRunspace.For(session.Runspace).Wiring.IsWired(HookKind.PreExecute).ShouldBeFalse();
  }

  [Fact]
  public void PreExecute_WithLineAcceptedHandlerSupport_ChainsExistingHandlerAndDispatches() {
    using var session = new KernelSession();
    session.Run(
      """
      $global:__fakeOption = [pscustomobject]@{ LineAcceptedHandler = [Action[string,int]]{ param($l, $c) $global:previousLine = $l } }
      function global:Get-PSReadLineOption { $global:__fakeOption }
      function global:Set-PSReadLineOption { param([Action[string,int]]$LineAcceptedHandler) $global:__fakeOption.LineAcceptedHandler = $LineAcceptedHandler }
      """);

    session.Run("Register-Hook PreExecute { $global:submitted = $_.CommandLine } | Out-Null");
    session.Run("$global:__fakeOption.LineAcceptedHandler.Invoke('Get-Process', 11)");

    session.Streams.Warning.ShouldBeEmpty();
    session.Global("previousLine").ShouldBe("Get-Process");
    session.Global("submitted").ShouldBe("Get-Process");
  }

  [Fact]
  public void Idle_RegisteredHook_RunsWhenEngineRaisesOnIdle() {
    using var session = new KernelSession();
    session.Run("Register-Hook Idle { $global:idleRan = $true } | Out-Null");

    session.Run("New-Event -SourceIdentifier ([System.Management.Automation.PSEngineEvent]::OnIdle) | Out-Null");

    session.Global("idleRan").ShouldBe(true);
  }

  private static string TempPathLiteral()
    => Path.GetTempPath().TrimEnd('\\', '/').Replace("'", "''");
}
