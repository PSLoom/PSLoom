// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using PSLoom.Warp.Kernel;

namespace PSLoom.Warp.Hosting;

/// <summary>
///   The entry point a harness module calls. A facade: the PSLoom kernel attaches the implementation when it loads, so a
///   harness never references the kernel assembly.
/// </summary>
public static class HarnessHost {
  private static IKernel? _kernel;

  internal static IKernel Kernel
    => Volatile.Read(ref _kernel) ?? throw WarpException.KernelUnavailable();

  internal static bool IsAttached
    => Volatile.Read(ref _kernel) is not null;

  /// <summary>
  ///   Registers a harness with the runspace importing the calling module. Call it from
  ///   <c>IModuleAssemblyInitializer.OnImport</c>; the kernel runs <see cref="IHarness.Compose" /> for that runspace.
  /// </summary>
  /// <typeparam name="THarness">The harness type, marked with <see cref="HarnessAttribute" />.</typeparam>
  /// <exception cref="WarpException">
  ///   The type has no <see cref="HarnessAttribute" /> (<c>WARP_HARNESS_ATTRIBUTE_MISSING</c>), was compiled against another
  ///   contract major (<c>WARP_CONTRACT_MISMATCH</c>), or the kernel is not loaded (<c>WARP_KERNEL_UNAVAILABLE</c>).
  /// </exception>
  public static void Register<THarness>() where THarness : class, IHarness, new() {
    var harnessType = typeof(THarness);
    var attribute = harnessType.GetCustomAttribute<HarnessAttribute>(false) ?? throw WarpException.HarnessAttributeMissing(harnessType);
    var compiledVersion = WarpContract.EnsureCompatible(harnessType.Assembly);

    Kernel.RegisterHarness(new HarnessRegistration(harnessType, attribute, compiledVersion, static () => new THarness()));
  }

  /// <summary>
  ///   Gets the runtime context of a harness already composed in the current runspace, for its cmdlet surface.
  /// </summary>
  /// <typeparam name="THarness">The harness type.</typeparam>
  /// <returns>The harness context for <c>Runspace.DefaultRunspace</c>.</returns>
  public static IHarnessContext Current<THarness>() where THarness : class, IHarness
    => Kernel.GetContext(typeof(THarness));

  internal static void Attach(IKernel kernel) {
    ArgumentNullException.ThrowIfNull(kernel);

    var existing = Interlocked.CompareExchange(ref _kernel, kernel, null);

    if (existing is not null &&
        !ReferenceEquals(existing, kernel)) {
      throw WarpException.KernelAlreadyAttached();
    }
  }
}
