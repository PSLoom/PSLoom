// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Management.Automation.Language;
using JetBrains.Annotations;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Sheds;
using PSLoom.Runtime.Verbs;
using PSLoom.Verbs;

namespace PSLoom.Tests.Runtime.Sheds;

[TestSubject(typeof(ShedQueue))]
public sealed class ShedQueueTests {
  [Theory]
  [InlineData("0a", "0b")]
  [InlineData("0c", "1a")]
  [InlineData("9c", "10a")]
  public void SlotsOrderByNumberThenLetter(string earlier, string later)
    => ShedQueue.SlotRank(earlier).ShouldBeLessThan(ShedQueue.SlotRank(later));

  [Fact]
  public void EntriesDrainBySlotThenDraftOrder() {
    var queue = new ShedQueue();
    var entries = Entries("Shed -Slot 1a\n'third'\nShed -Wait\n'first'\nShed -Slot 0a\n'second'");

    foreach (var entry in entries) {
      queue.Enqueue(entry);
    }

    var applied = new List<int>();
    queue.DrainAll(entry => {
      applied.Add(entry.Line);
      return true;
    });

    applied.ShouldBe([4, 6, 2]);
  }

  [Fact]
  public void ASliceStopsWhenTimeRunsOut_ButAlwaysAppliesOne() {
    var queue = new ShedQueue();

    foreach (var entry in Entries("Shed -Wait\n1\nShed -Wait\n2\nShed -Wait\n3")) {
      queue.Enqueue(entry);
    }

    long now = 0;
    var slice = TimeSpan.FromMilliseconds(15);

    // Each apply takes 10 ms of fake time: the first fits, the second crosses the limit, the third waits for the next firing.
    queue.DrainSlice(_ => {
        now += TimeSpan.FromMilliseconds(10).Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
        return true;
      },
      () => now, slice).ShouldBe(2);
    queue.Count.ShouldBe(1);

    queue.DrainSlice(_ => {
        now += TimeSpan.FromMilliseconds(40).Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
        return true;
      },
      () => now, slice).ShouldBe(1);
  }

  [Fact]
  public void RemovingAnEntryTakesItOutOfTheQueue() {
    var queue = new ShedQueue();
    var entries = Entries("Shed -Wait\n1\nShed -Wait\n2");
    queue.Enqueue(entries[0]);
    queue.Enqueue(entries[1]);

    queue.Remove(entries[0]).ShouldBeTrue();

    queue.Count.ShouldBe(1);
  }

  private static IReadOnlyList<ShedEntry> Entries(string draft) {
    var registry = new VerbRegistry();
    registry.Add(typeof(StyleVerb), LoomSession.KERNEL_OWNER);
    var run = new LoomRun(true);

    return [
      .. ShedAnalyzer.Analyze(Parser.ParseInput(draft, out _, out _), registry).Declarations
        .Select(declaration => new ShedEntry(declaration, null, run.DisambiguateKey($"k{declaration.Index}"), UInt128.Zero))
    ];
  }
}
