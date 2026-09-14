// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using BenchmarkDotNet.Running;

namespace PSLoom.Benchmarks;

/// <summary>
///   Entry point for the BenchmarkDotNet suites; pass <c>--filter</c> to select suites.
/// </summary>
public static class Program {
  /// <summary>
  ///   Runs the benchmark switcher over every suite in this assembly.
  /// </summary>
  /// <param name="args">BenchmarkDotNet command-line arguments.</param>
  public static void Main(string[] args)
    => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
