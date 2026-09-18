// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;
using BenchmarkDotNet.Attributes;
using PSLoom.Runtime.Hooks;
using PSLoom.Warp.Hooks;

namespace PSLoom.Benchmarks.Hooks;

/// <summary>
///   Hook dispatch. Budget: no handlers under 10 ns with zero allocation.
/// </summary>
[MemoryDiagnoser]
public class HookDispatchBenchmarks {
  private HookBus _bus = null!;
  private Runspace _runspace = null!;

  /// <summary>
  ///   Opens a runspace and registers one C# handler on <see cref="HookKind.PrePrompt" />.
  /// </summary>
  [GlobalSetup]
  public void Setup() {
    _runspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
    _runspace.Open();
    Runspace.DefaultRunspace = _runspace;

    _bus = new HookBus(_runspace);
    _bus.Subscribe(HookKind.PrePrompt, static _ => { });
  }

  /// <summary>
  ///   Closes the runspace.
  /// </summary>
  [GlobalCleanup]
  public void Cleanup()
    => _runspace.Dispose();

  /// <summary>
  ///   A kind nobody subscribed to.
  /// </summary>
  [Benchmark(Baseline = true)]
  public void DispatchWithoutHandlers()
    => _bus.Dispatch(HookKind.Idle);

  /// <summary>
  ///   One C# handler, including timing and the diagnostic entry.
  /// </summary>
  [Benchmark]
  public void DispatchOneHandler()
    => _bus.Dispatch(HookKind.PrePrompt);
}
