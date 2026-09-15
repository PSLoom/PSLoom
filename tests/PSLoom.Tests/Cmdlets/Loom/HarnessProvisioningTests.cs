// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Loom;
using PSLoom.Runtime.Harnesses;
using PSLoom.Runtime.Loom;
using PSLoom.Tests.Utility;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(HarnessProvisioner))]
[TestSubject(typeof(PowerShellHarnessModules))]
[TestSubject(typeof(InvokeLoomCmdlet))]
public sealed class HarnessProvisioningTests {
  [Fact]
  public void NotFirstParty_IsRefusedWithoutTouchingModules() {
    using var session = new KernelSession();
    var modules = UseFake(session, installed: true);

    session.Run("Invoke-Loom { Thread NotReal; Style 'a:*' 'b' 1 }");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_NOT_FIRST_PARTY);
    error.Exception.Message.ShouldContain("Reed");
    modules.Calls.ShouldBeEmpty();
    session.Style("a:x", "b").ShouldBeNull();
  }

  [Fact]
  public void InstalledHarness_IsImportedWithoutInstalling() {
    using var session = new KernelSession();
    var modules = UseFake(session, installed: true);

    session.Run("Invoke-Loom { Thread Crates; Crate c { Bottle x } }");

    session.Streams.Error.ShouldBeEmpty();
    modules.Calls.ShouldBe(["Import PSLoom.Crates"]);
    modules.Progress.ShouldBeEmpty();
    CrateVerb.Built.ShouldContain(crate => crate.Name == "c" && crate.Contents.Contains("x"));
  }

  [Fact]
  public void MissingHarness_IsInstalledUnderTheLock_ThenImported() {
    using var session = new KernelSession();
    var modules = UseFake(session, installed: false);

    session.Run("Invoke-Loom { Thread Crates; Crate installed { Bottle y } }");

    session.Streams.Error.ShouldBeEmpty();
    modules.Calls.ShouldBe(["Import PSLoom.Crates", "Import PSLoom.Crates", "Install PSLoom.Crates", "Import PSLoom.Crates"]);
    modules.Progress.ShouldContain("Installing PSLoom.Crates…");
    session.Loom.LastRun!.ShouldContain(timing => timing.Phase == LoomPhase.Install && timing.Name == "Crates");
    CrateVerb.Built.ShouldContain(crate => crate.Name == "installed");
  }

  [Fact]
  public void InstallFailure_ReportsReasonAndManualCommand_AndDoesNotExecute() {
    using var session = new KernelSession();
    var modules = UseFake(session, installed: false);
    modules.InstallFailure = new InvalidOperationException("network unreachable");

    session.Run("Invoke-Loom { Thread Crates -Version 1.0.0; Style 'a:*' 'b' 1 }");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_INSTALL_FAILED);
    error.Exception.Message.ShouldContain("network unreachable");
    error.Exception.Message.ShouldContain("Install-PSResource -Name PSLoom.Crates -Version 1.0.0 -Repository PSGallery -Scope CurrentUser");
    session.Style("a:x", "b").ShouldBeNull();
    session.Loom.IsWoven.ShouldBeFalse();
  }

  [Fact]
  public void Validate_NeverInstalls() {
    using var session = new KernelSession();
    var modules = UseFake(session, installed: false);

    session.Run("Invoke-Loom -Validate { Thread Crates }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_NOT_INSTALLED);
    modules.Calls.ShouldBe(["Import PSLoom.Crates"]);
  }

  [Fact]
  public void PinnedVersion_IsPassedToImportAndInstall() {
    using var session = new KernelSession();
    var modules = UseFake(session, installed: false);
    var version = typeof(CratesHarness).Assembly.GetName().Version!;

    session.Run($"Invoke-Loom {{ Thread Crates -Version {version.ToString(3)} }}");

    session.Streams.Error.ShouldBeEmpty();
    modules.Calls.ShouldContain($"Install PSLoom.Crates {version.ToString(3)}");
  }

  [Fact]
  public void PinnedVersionDifferentFromLoadedHarness_IsAConflict() {
    using var session = new KernelSession();
    UseFake(session, installed: true);

    session.Run("Invoke-Loom -Validate { Thread Crates }");
    session.Run("Invoke-Loom { Thread Crates -Version 99.0.0 }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_VERSION_CONFLICT);
  }

  [Fact]
  public void RealScriptPath_CallsInstallPSResourceWithTrustedGalleryForCurrentUser() {
    using var session = new KernelSession();
    session.Run(
      """
      function global:Install-PSResource {
        [CmdletBinding()]
        param($Name, $Repository, $Scope, [switch]$TrustRepository, $Version)
        $global:installArgs = $PSBoundParameters
        throw 'network unreachable'
      }
      """);

    session.Run("Invoke-Loom { Thread Colorway -Version 2.0.0 }");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_INSTALL_FAILED);
    error.Exception.Message.ShouldContain("network unreachable");
    session.Streams.Information.ShouldContain(record => record.MessageData.ToString()!.Contains("Installing PSLoom.Colorway 2.0.0"));

    var arguments = session.Run("$global:installArgs.GetEnumerator() | ForEach-Object { '{0}={1}' -f $_.Key, $_.Value }")
      .Select(item => item.ToString()).ToArray();
    arguments.ShouldContain("Name=PSLoom.Colorway");
    arguments.ShouldContain("Repository=PSGallery");
    arguments.ShouldContain("Scope=CurrentUser");
    arguments.ShouldContain("TrustRepository=True");
    arguments.ShouldContain("Version=2.0.0");
  }

  private static FakeHarnessModules UseFake(KernelSession session, bool installed) {
    var modules = new FakeHarnessModules { Installed = installed };
    session.Loom.FirstParty.Add("Crates");
    session.Loom.HarnessModulesFactory = _ => modules;
    return modules;
  }
}
