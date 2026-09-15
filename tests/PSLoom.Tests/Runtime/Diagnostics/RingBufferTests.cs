// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Diagnostics;

namespace PSLoom.Tests.Runtime.Diagnostics;

[TestSubject(typeof(RingBuffer<>))]
public sealed class RingBufferTests {
  [Fact]
  public void Snapshot_BelowCapacity_ReturnsEntriesOldestFirst() {
    var buffer = new RingBuffer<string>(3);
    buffer.Record("a");
    buffer.Record("b");

    buffer.Snapshot().ShouldBe(["a", "b"]);
    buffer.Count.ShouldBe(2);
  }

  [Fact]
  public void Record_BeyondCapacity_DropsOldestEntries() {
    var buffer = new RingBuffer<string>(3);

    foreach (var entry in new[] { "a", "b", "c", "d", "e" }) {
      buffer.Record(entry);
    }

    buffer.Snapshot().ShouldBe(["c", "d", "e"]);
    buffer.Count.ShouldBe(3);
  }

  [Theory]
  [InlineData(0, new string[0])]
  [InlineData(2, new[] { "d", "e" })]
  [InlineData(10, new[] { "c", "d", "e" })]
  public void Snapshot_WithLast_ReturnsMostRecentEntries(int last, string[] expected) {
    var buffer = new RingBuffer<string>(3);

    foreach (var entry in new[] { "a", "b", "c", "d", "e" }) {
      buffer.Record(entry);
    }

    buffer.Snapshot(last).ShouldBe(expected);
  }

  [Fact]
  public void Clear_RemovesEveryEntry() {
    var buffer = new RingBuffer<string>(2);
    buffer.Record("a");

    buffer.Clear();

    buffer.Snapshot().ShouldBeEmpty();
    buffer.Count.ShouldBe(0);
  }

  [Fact]
  public void Constructor_NonPositiveCapacity_Throws() {
    Should.Throw<ArgumentOutOfRangeException>(() => new RingBuffer<string>(0));
  }
}
