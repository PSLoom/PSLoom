// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Treadles;
using PSLoom.Tests.Utility;
using PSLoom.Verbs;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(TreadleVerb))]
public sealed class TreadleVerbTests {
  [Fact]
  public void ADraftDefinesTheFunction() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Treadle echoArgs { Write-Output 'first' } }");

    session.Streams.Error.ShouldBeEmpty();
    session.Run("echoArgs second").Select(result => result.BaseObject).ShouldBe(["first", "second"]);
  }

  [Fact]
  public void Validate_DefinesNothing() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom -Validate { Treadle echoArgs { Write-Output 'first' } }");

    session.Run("Test-Path function:echoArgs").Single().BaseObject.ShouldBe(false);
  }

  [Fact]
  public void ACollisionIsReported_AndTheDraftKeepsGoing() {
    using var session = new KernelSession();
    session.Run("function taken { 'original' }");

    session.Run("Invoke-Loom { Treadle taken { Write-Output 'treadle' }; Style 'app:*' 'color' 'Cyan' }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(TreadleException.NAME_COLLISION);
    session.Run("taken").Single().BaseObject.ShouldBe("original");
    session.Style("app:main", "color").ShouldBe("Cyan");
  }

  [Fact]
  public void AnInvalidBodyIsReported() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Treadle glog { git log; git status } }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(TreadleException.BODY_NOT_SINGLE_COMMAND);
  }

  [Fact]
  public void Reweave_ReplacesAChangedTreadle_AndRevertsARemovedOne() {
    using var session = new KernelSession();
    session.Run("Invoke-Loom { Treadle echoArgs { Write-Output 'first' }; Treadle other { Write-Output 'other' } }");

    session.Run("Invoke-Loom -Reweave { Treadle echoArgs { Write-Output 'second' } }");

    session.Streams.Error.ShouldBeEmpty();
    session.Streams.Warning.ShouldBeEmpty();
    session.Run("echoArgs").Single().BaseObject.ShouldBe("second");
    session.Run("Test-Path function:other").Single().BaseObject.ShouldBe(false);
  }

  [Fact]
  public void Reweave_LeavesAnUnchangedTreadleAlone() {
    using var session = new KernelSession();
    const string DRAFT = "Treadle echoArgs { Write-Output 'first' }";
    session.Run($"Invoke-Loom {{ {DRAFT} }}");
    session.Run("function global:echoArgs { 'hand-edited' }");

    session.Run($"Invoke-Loom -Reweave {{ {DRAFT} }}");

    session.Run("echoArgs").Single().BaseObject.ShouldBe("hand-edited");
  }
}
