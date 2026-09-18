// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using BenchmarkDotNet.Attributes;
using PSLoom.Runtime.Styles;

namespace PSLoom.Benchmarks.Styles;

/// <summary>
///   Style resolution. Budget: a cache hit under 50 ns with zero allocation.
/// </summary>
[MemoryDiagnoser]
public class StyleResolveBenchmarks {
  private StyleStore _store = null!;

  /// <summary>
  ///   Builds a store with a realistic spread of definitions and warms the caches.
  /// </summary>
  [GlobalSetup]
  public void Setup() {
    _store = new StyleStore();

    for (var index = 0; index < 50; index++) {
      _store.Set($"harness{index}:*", "enabled", index % 2 == 0);
      _store.Set($"harness{index}:theme:*", "color", $"Color{index}");
    }

    _store.Set("colorway:theme:colors:keyword", "fg", "Blue");
    _store.Set("colorway:*", "fg", "Gray");
    _store.Set("colorway:th?me:*", "fg", "Green");

    ResolveExactHit();
    ResolvePrefixHit();
    ResolveWildcardHit();
  }

  /// <summary>
  ///   Cache hit whose winner is a fully literal pattern.
  /// </summary>
  [Benchmark(Baseline = true)]
  public object? ResolveExactHit()
    => _store.Resolve("colorway:theme:colors:keyword", "fg");

  /// <summary>
  ///   Cache hit whose winner is a trailing-star pattern.
  /// </summary>
  [Benchmark]
  public object? ResolvePrefixHit()
    => _store.Resolve("harness42:theme:dark", "color");

  /// <summary>
  ///   Cache hit whose winner is a general wildcard pattern.
  /// </summary>
  [Benchmark]
  public object? ResolveWildcardHit()
    => _store.Resolve("colorway:theme:colors:string", "fg");

  /// <summary>
  ///   Unknown style name: a single dictionary miss.
  /// </summary>
  [Benchmark]
  public object? ResolveUnknownName()
    => _store.Resolve("colorway:theme", "does-not-exist");
}
