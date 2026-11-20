// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using PSLoom.TestKit;

namespace PSLoom.Tests.Module;

/// <summary>Guards the promise that importing the base is cheap, because every harness pulls it in.</summary>
public sealed class ModuleStartupBudgetTests {
  // Three times the median import time measured when the budget was last set, rounded up to 50 ms, with a floor of 250 ms.
  private const double IMPORT_BUDGET_MILLISECONDS = 250;
  private const int ATTEMPTS = 5;

  private readonly static string[] ModulePaths = [RepoLayout.ModulesDirectory(RepoLayout.FindRoot())];

  [Fact]
  public void ImportStaysWithinTheBudget() {
    var best = Enumerable.Range(0, ATTEMPTS).Min(_ => MeasureImport());

    Assert.True(best <= IMPORT_BUDGET_MILLISECONDS,
      string.Create(CultureInfo.InvariantCulture,
        $"The fastest of {ATTEMPTS} imports took {best:F0} ms; the budget is {IMPORT_BUDGET_MILLISECONDS:F0} ms."));
  }

  private static double MeasureImport() {
    var result =
      PwshProcess.Run(
        "(Measure-Command { Import-Module PSLoom }).TotalMilliseconds.ToString([cultureinfo]::InvariantCulture)",
        ModulePaths);

    Assert.Equal(0, result.ExitCode);

    return double.Parse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture);
  }
}
