// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Styles;
using PSLoom.Runtime.Styles;
using PSLoom.Tests.Utility;

namespace PSLoom.Tests.Cmdlets.Styles;

[TestSubject(typeof(RegisterStyleWatcherCmdlet))]
[TestSubject(typeof(UnregisterStyleWatcherCmdlet))]
[TestSubject(typeof(TraceStyleCmdlet))]
public sealed class StyleWatcherCmdletTests {
  [Fact]
  public void Watcher_ReceivesChangeAsDollarUnderscoreAndFirstArgument() {
    using var session = new KernelSession();
    session.Run("Register-StyleWatcher 'colorway:theme' 'enabled' { $global:newValue = $_.NewValue; $global:context = $args[0].Context } | Out-Null");

    session.Run("Set-Style 'colorway:*' 'enabled' $true");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Global("newValue").ShouldBe(true);
    session.Global("context").ShouldBe("colorway:theme");
  }

  [Fact]
  public void Register_ReturnsRegistration() {
    using var session = new KernelSession();

    var registration = session.Run("Register-StyleWatcher 'app:*' 'color' { } -Pattern").ShouldHaveSingleItem().BaseObject
      .ShouldBeOfType<StyleWatcherRegistration>();

    registration.IsPattern.ShouldBeTrue();
    registration.Context.ShouldBe("app:*");
    registration.Name.ShouldBe("color");
  }

  [Fact]
  public void FailingWatcher_WritesNonTerminatingErrorAndOthersStillRun() {
    using var session = new KernelSession();
    session.Run("Register-StyleWatcher 'app:main' 'color' { throw 'boom' } | Out-Null");
    session.Run("Register-StyleWatcher 'app:main' 'color' { $global:secondRan = $true } | Out-Null");

    var output = session.Run("Set-Style 'app:*' 'color' 'Cyan'; 'after'");

    output.ShouldHaveSingleItem().BaseObject.ShouldBe("after");
    session.Global("secondRan").ShouldBe(true);
    session.Streams.Error.ShouldContain(error => error.FullyQualifiedErrorId.StartsWith(StyleException.WATCHER_FAILED));
    session.Run("Get-Style 'app:main' 'color'").ShouldHaveSingleItem().BaseObject.ShouldBe("Cyan");
  }

  [Fact]
  public void Replay_RunsImmediatelyWithCurrentValue() {
    using var session = new KernelSession();
    session.Run("Set-Style 'app:*' 'color' 'Cyan'");

    session.Run("Register-StyleWatcher 'app:main' 'color' { $global:replayed = $_.NewValue } -Replay | Out-Null");

    session.Global("replayed").ShouldBe("Cyan");
  }

  [Fact]
  public void Replay_PatternWatcher_RunsOncePerMatchingDefinition() {
    // Harvested: StyleWatcherDispatchTests.Replay_PatternWatcher_FiresOncePerCurrentlyMatchingDefinition.
    using var session = new KernelSession();
    session.Run("Set-Style 'colorway:a' 'theme' 'A'; Set-Style 'colorway:b' 'theme' 'B'; Set-Style 'other:c' 'theme' 'C'");

    session.Run("$global:replays = @(); Register-StyleWatcher 'colorway:*' 'theme' { $global:replays += \"$($_.Context)=$($_.NewValue)\" } -Pattern -Replay | Out-Null");

    session.Run("$global:replays").Select(result => result.BaseObject).ShouldBe(["colorway:a=A", "colorway:b=B"]);
  }

  [Fact]
  public void Replay_Failure_WritesErrorButStillRegisters() {
    using var session = new KernelSession();
    session.Run("Set-Style 'app:*' 'color' 'Cyan'");

    var output = session.Run("Register-StyleWatcher 'app:main' 'color' { throw 'boom' } -Replay");

    output.ShouldHaveSingleItem().BaseObject.ShouldBeOfType<StyleWatcherRegistration>();
    session.Streams.Error.ShouldNotBeEmpty();
  }

  [Fact]
  public void Unregister_PipedFromRegister_StopsWatcher() {
    using var session = new KernelSession();
    session.Run("$global:w = Register-StyleWatcher 'app:main' 'color' { $global:ran = $true }");

    session.Run("$global:w | Unregister-StyleWatcher");
    session.Run("Set-Style 'app:*' 'color' 'Cyan'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Global("ran").ShouldBeNull();
  }

  [Fact]
  public void Unregister_UnknownId_WritesWarning() {
    using var session = new KernelSession();

    session.Run($"Unregister-StyleWatcher '{Guid.NewGuid()}'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Streams.Warning.ShouldNotBeEmpty();
  }

  [Fact]
  public void TraceStyle_ReturnsWatcherRuns_WithLast() {
    using var session = new KernelSession();
    session.Run("Register-StyleWatcher 'app:*' 'color' { throw 'boom' } -Pattern | Out-Null");
    session.Run("Set-Style 'app:*' 'color' 'Cyan'; Set-Style 'app:*' 'color' 'Red'");

    var entries = session.Run("Trace-Style").Select(entry => entry.BaseObject).Cast<StyleDiagnosticEntry>().ToArray();
    entries.Length.ShouldBe(2);
    entries.ShouldAllBe(entry => entry.Exception != null);

    session.Run("Trace-Style -Last 1").ShouldHaveSingleItem().BaseObject.ShouldBeOfType<StyleDiagnosticEntry>().NewValue.ShouldBe("Red");
  }
}
