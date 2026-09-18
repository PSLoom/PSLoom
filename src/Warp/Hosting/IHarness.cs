// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Hosting;

/// <summary>
///   A PSLoom extension. Mark the implementation with <see cref="HarnessAttribute" /> and register it with
///   <see cref="HarnessHost.Register{THarness}" /> from the module's <c>IModuleAssemblyInitializer.OnImport</c>.
/// </summary>
public interface IHarness {
  /// <summary>
  ///   Contributes verbs, watchers and hook handlers. Runs once per runspace that imports the harness.
  /// </summary>
  /// <param name="builder">The capabilities available to the harness, scoped to it and to the importing runspace.</param>
  void Compose(IHarnessBuilder builder);
}
