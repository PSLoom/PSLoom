// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Treadles;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Treadles;

namespace PSLoom.Tests.Runtime.Treadles;

[TestSubject(typeof(TreadleCatalog))]
public sealed class TreadleCatalogTests {
  [Fact]
  public void Changed_ReportsPreviousAndCurrent() {
    using var session = new KernelSession();
    var catalog = TreadleCatalog.PerRunspace.For(session.Runspace);
    var changes = new List<TreadleChangedEventArgs>();
    catalog.Changed += (_, change) => changes.Add(change);

    catalog.Set(session.Engine(), "glog", TreadleBody.Parse("git log"), false);
    catalog.Set(session.Engine(), "glog", TreadleBody.Parse("git log --graph"), false);
    catalog.Remove(session.Engine(), "glog");

    changes.Select(change => (change.Previous?.BakedTokens.Count, change.Current?.BakedTokens.Count)).ShouldBe([(null, 1), (1, 2), (2, null)]);
  }

  [Fact]
  public void AFailingSubscriber_NeitherBlocksTheWriteNorTheOthers() {
    using var session = new KernelSession();
    var catalog = TreadleCatalog.PerRunspace.For(session.Runspace);
    var reached = false;
    catalog.Changed += (_, _) => throw new InvalidOperationException("subscriber blew up");
    catalog.Changed += (_, _) => reached = true;

    var outcome = catalog.Set(session.Engine(), "glog", TreadleBody.Parse("git log"), false);

    reached.ShouldBeTrue();
    outcome.SubscriberFailures.ShouldHaveSingleItem().Message.ShouldBe("subscriber blew up");
    catalog.TryGet("GLOG", out var definition).ShouldBeTrue();
    definition!.TargetCommand.ShouldBe("git");
  }

  [Fact]
  public void ASubscriberFailure_IsReportedByTheCmdlet() {
    using var session = new KernelSession();
    TreadleCatalog.PerRunspace.For(session.Runspace).Changed += (_, _) => throw new InvalidOperationException("subscriber blew up");

    session.Run("New-Treadle glog { git log }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(TreadleException.SUBSCRIBER_FAILED);
    session.Run("Test-Path function:glog").Single().BaseObject.ShouldBe(true);
  }
}
