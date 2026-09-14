// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using PSLoom.TestKit;

namespace PSLoom.Warp.Tests.Architecture;

public sealed class WarpDependencyTests {
  private const string WARP_PROJECT = "src/Warp/Warp.csproj";

  [Fact]
  public void WarpHasNoProjectReferences() {
    var project = RepositoryLayout.LoadProject(WARP_PROJECT);

    project.Descendants("ProjectReference").ShouldBeEmpty();
  }

  [Fact]
  public void WarpReferencesOnlyTheRuntimeAndSystemManagementAutomation() {
    var references = LoadWarp().GetReferencedAssemblies().Select(reference => reference.Name!);

    references.ShouldAllBe(name => name == "netstandard" || name == "System.Management.Automation" || name.StartsWith("System."));
  }

  [Fact]
  public void WarpAssemblyVersionIsTheContractMajorOnly() {
    var version = LoadWarp().GetName().Version!;

    version.Minor.ShouldBe(0);
    version.Build.ShouldBe(0);
    version.Revision.ShouldBe(0);
  }

  private static Assembly LoadWarp()
    => Assembly.Load(new AssemblyName("Warp"));
}
