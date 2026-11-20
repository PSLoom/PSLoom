// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.ManifestTool.Tests.Samples;

namespace PSLoom.ManifestTool.Tests;

public sealed class CmdletScannerTests {
  [Fact]
  public void ScanReturnsTheCmdletNamesInOrder() {
    string[] expected = ["Get-Sample", "Invoke-Sample"];

    var names = CmdletScanner.Scan(typeof(GetSampleCmdlet).Assembly.Location);

    Assert.Equal(expected, names);
  }

  [Fact]
  public void ScanOfAnAssemblyWithoutCmdletsIsEmpty() {
    var names = CmdletScanner.Scan(typeof(CmdletScanner).Assembly.Location);

    Assert.Empty(names);
  }
}
