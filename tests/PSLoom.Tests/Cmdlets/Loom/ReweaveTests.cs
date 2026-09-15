// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Loom;
using PSLoom.Runtime.Loom;
using PSLoom.Tests.Utility;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(InvokeLoomCmdlet))]
public sealed class ReweaveTests {
  private const string DRAFT =
    """
    Thread Fixture
    Style 'app:*' 'color' 'Cyan'
    Style 'app:*' 'size' 12
    Box tools { Item hammer }
    """;

  [Fact]
  public void UnchangedDraft_WeavesNothingAgain() {
    using var session = Woven(DRAFT);

    Reweave(session, DRAFT);

    session.Streams.Error.ShouldBeEmpty();
    Writes(session).ShouldBe(0);
  }

  [Fact]
  public void ChangedStyle_IsUpserted_AndUnchangedVerbsAreSkipped() {
    using var session = Woven(DRAFT);

    Reweave(session, DRAFT.Replace("'Cyan'", "'Red'"));

    session.Style("app:main", "color").ShouldBe("Red");
    Writes(session).ShouldBe(1);
  }

  [Fact]
  public void RemovedStyle_IsReverted() {
    using var session = Woven(DRAFT);

    Reweave(session, DRAFT.Replace("Style 'app:*' 'size' 12", string.Empty));

    session.Streams.Error.ShouldBeEmpty();
    session.Style("app:main", "size").ShouldBeNull();
    session.Style("app:main", "color").ShouldBe("Cyan");
  }

  [Fact]
  public void RemovedStatements_AreDescribedByVerbAndKeyValues() {
    using var session = Woven("Style 'app:*' 'window title' 'x'\nStyle 'app:*' 'size' 12");

    session.Run("Invoke-Loom -Reweave { } -Verbose");

    session.Streams.Verbose.Select(record => record.Message)
      .ShouldBe(["Undid removed statement: Style app:* size.", "Undid removed statement: Style app:* 'window title'."]);
  }

  [Fact]
  public void RemovedThread_WarnsThatARestartIsNeeded() {
    using var session = Woven(DRAFT);

    Reweave(session, "Style 'app:*' 'color' 'Cyan'\nStyle 'app:*' 'size' 12");

    session.Streams.Warning.ShouldContain(record =>
      record.Message.Contains(LoomException.REWEAVE_REQUIRES_RESTART) && record.Message.Contains("Thread") && record.Message.Contains("Box"));
  }

  [Fact]
  public void NewVerb_Weaves() {
    using var session = Woven(DRAFT);

    Reweave(session, DRAFT + "\nBox extra { Item saw }");

    session.Style("fixture:box:extra", "items").ShouldBe("saw");
    Writes(session).ShouldBe(1);
  }

  [Fact]
  public void FailedVerb_IsRetriedOnTheNextReweave() {
    using var session = Woven(DRAFT + "\nFail 'still broken'");
    session.Streams.Error.ShouldHaveSingleItem();
    session.Streams.Error.Clear();

    Reweave(session, DRAFT + "\nFail 'still broken'");

    session.Streams.Error.ShouldHaveSingleItem().Exception.Message.ShouldBe("still broken");
  }

  [Fact]
  public void ValidationError_LeavesStateUntouched() {
    using var session = Woven(DRAFT);

    Reweave(session, "Style 'app:*' 'color' 'Red'\nItem stray");
    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(LoomException.VERB_OUT_OF_SCOPE);
    session.Style("app:main", "color").ShouldBe("Cyan");

    Reweave(session, DRAFT);
    Writes(session).ShouldBe(0);
  }

  [Fact]
  public void NonVerbStatements_RunAgain() {
    const string DRAFT_WITH_STATEMENT = "$global:runs = ([int]$global:runs) + 1\nStyle 'app:*' 'color' 'Cyan'";
    using var session = Woven(DRAFT_WITH_STATEMENT);

    Reweave(session, DRAFT_WITH_STATEMENT);

    session.Global("runs").ShouldBe(2);
  }

  [Fact]
  public void SessionStarting_IsNotRaisedAgain() {
    using var session = Woven(DRAFT);

    Reweave(session, DRAFT.Replace("'Cyan'", "'Red'"));

    session.Style("fixture:events", "session-starting").ShouldBe(1);
  }

  [Fact]
  public void ReweaveOnUnwovenSession_IsAFirstRun() {
    using var session = new KernelSession();

    Reweave(session, DRAFT);

    session.Loom.IsWoven.ShouldBeTrue();
    session.Style("app:main", "color").ShouldBe("Cyan");
    session.Style("fixture:events", "session-starting").ShouldBe(1);
  }

  [Fact]
  public void ValidateWithReweave_OnlyValidates() {
    using var session = Woven(DRAFT);

    session.Run($"Invoke-Loom -Reweave -Validate {{\n{DRAFT.Replace("'Cyan'", "'Red'")}\n}}");

    session.Style("app:main", "color").ShouldBe("Cyan");
  }

  private static KernelSession Woven(string draft) {
    var session = new KernelSession();
    session.Run($"Invoke-Loom {{\n{draft}\n}}");
    session.Run(
      "$global:writes = 0; Register-StyleWatcher 'app:*' 'color' { $global:writes++ } -Pattern | Out-Null; " +
      "Register-StyleWatcher 'app:*' 'size' { $global:writes++ } -Pattern | Out-Null; " +
      "Register-StyleWatcher 'fixture:box:*' 'items' { $global:writes++ } -Pattern | Out-Null");
    return session;
  }

  private static void Reweave(KernelSession session, string draft)
    => session.Run($"Invoke-Loom -Reweave {{\n{draft}\n}}");

  private static int Writes(KernelSession session)
    => (int)(session.Global("writes") ?? 0);
}
