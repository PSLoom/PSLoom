// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Loom;
using PSLoom.Runtime.Loom;
using PSLoom.Tests.Utility;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(UpdateHarnessCmdlet))]
public sealed class UpdateHarnessCmdletTests {
  // Stand-ins for PSResourceGet: Reed is installed, nothing else is; updates are recorded.
  private const string PSRESOURCEGET_STANDINS =
    """
    $global:updates = [System.Collections.Generic.List[string]]::new()
    function global:Get-InstalledPSResource {
      [CmdletBinding()] param($Name, $Scope)
      if ($Name -eq 'PSLoom.Reed') { [pscustomobject]@{ Name = $Name; Version = '1.0.0' } }
    }
    function global:Update-PSResource {
      [CmdletBinding()] param($Name, $Repository, $Scope, [switch]$TrustRepository, [switch]$PassThru)
      if ($global:failUpdate) { throw 'gallery unavailable' }
      $global:updates.Add("$Name|$Repository|$Scope|$TrustRepository|$PassThru")
      [pscustomobject]@{ Name = $Name; Version = '1.1.0' }
    }
    """;

  [Fact]
  public void ExplicitInstalledHarness_IsUpdatedFromTrustedGallery_AndWarnsAboutNextSession() {
    using var session = NewSession();

    var output = session.Run("Update-Harness Reed");

    session.Streams.Error.ShouldBeEmpty();
    session.Run("$global:updates").Select(item => item.ToString()).ShouldBe(["PSLoom.Reed|PSGallery|CurrentUser|True|True"]);
    output.ShouldHaveSingleItem().Properties["Version"].Value.ShouldBe("1.1.0");
    session.Streams.Warning.ShouldContain(record => record.Message.Contains("next PowerShell session"));
  }

  [Fact]
  public void NoNames_UpdatesOnlyInstalledFirstPartyHarnesses_Silently() {
    using var session = NewSession();

    session.Run("Update-Harness");

    session.Run("$global:updates").Select(item => item.ToString()).ShouldBe(["PSLoom.Reed|PSGallery|CurrentUser|True|True"]);
    session.Streams.Warning.ShouldNotContain(record => record.Message.Contains("not installed"));
  }

  [Fact]
  public void ExplicitNotInstalledHarness_WarnsAndDoesNotUpdate() {
    using var session = NewSession();

    session.Run("Update-Harness Weft");

    session.Streams.Warning.ShouldContain(record => record.Message.Contains("PSLoom.Weft is not installed"));
    session.Run("$global:updates.Count").ShouldHaveSingleItem().BaseObject.ShouldBe(0);
  }

  [Fact]
  public void NotFirstParty_IsAnError() {
    using var session = NewSession();

    session.Run("Update-Harness NotReal");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_NOT_FIRST_PARTY);
  }

  [Fact]
  public void WhatIf_DoesNotUpdate() {
    using var session = NewSession();

    session.Run("Update-Harness Reed -WhatIf");

    session.Run("$global:updates.Count").ShouldHaveSingleItem().BaseObject.ShouldBe(0);
  }

  [Fact]
  public void UpdateFailure_IsReported() {
    using var session = NewSession();
    session.Run("$global:failUpdate = $true");

    session.Run("Update-Harness Reed");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_UPDATE_FAILED);
    error.Exception.Message.ShouldContain("gallery unavailable");
  }

  private static KernelSession NewSession() {
    var session = new KernelSession();
    session.Run(PSRESOURCEGET_STANDINS);
    return session;
  }
}
