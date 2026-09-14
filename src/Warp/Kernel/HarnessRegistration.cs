// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Hosting;

namespace PSLoom.Warp.Kernel;

/// <summary>
///   A validated harness registration handed from the facade to the kernel.
/// </summary>
internal sealed class HarnessRegistration(Type harnessType, HarnessAttribute attribute, Version compiledWarpVersion, Func<IHarness> factory) {
  public Type HarnessType { get; } = harnessType;

  public HarnessAttribute Attribute { get; } = attribute;

  public Version CompiledWarpVersion { get; } = compiledWarpVersion;

  public Func<IHarness> Factory { get; } = factory;
}
