// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Harnesses;
using PSLoom.Warp.Hosting;

namespace PSLoom.Tests.Utility;

/// <summary>
///   Module operations for provisioning tests: nothing is installed until <see cref="Install" /> runs, and a successful import
///   registers <see cref="CratesHarness" /> the way a module's initializer would.
/// </summary>
internal sealed class FakeHarnessModules : IHarnessModules {
  public List<string> Calls { get; } = [];

  public List<string> Progress { get; } = [];

  public bool Installed { get; set; }

  public Exception? InstallFailure { get; set; }

  public ModuleImportResult Import(string moduleName, Version? version) {
    Calls.Add($"Import {moduleName}{Suffix(version)}");

    if (!Installed) {
      return new ModuleImportResult(ModuleImportStatus.NotFound, new InvalidOperationException("not found"));
    }

    HarnessHost.Register<CratesHarness>();
    return new ModuleImportResult(ModuleImportStatus.Imported);
  }

  public void Install(string moduleName, Version? version) {
    Calls.Add($"Install {moduleName}{Suffix(version)}");

    if (InstallFailure is not null) {
      throw InstallFailure;
    }

    Installed = true;
  }

  public void WriteProgress(string message)
    => Progress.Add(message);

  private static string Suffix(Version? version)
    => version is null ? string.Empty : $" {version}";
}
