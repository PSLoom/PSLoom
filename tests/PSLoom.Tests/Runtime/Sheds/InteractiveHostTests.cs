// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Sheds;

namespace PSLoom.Tests.Runtime.Sheds;

[TestSubject(typeof(InteractiveHost))]
public sealed class InteractiveHostTests {
  [Theory]
  [InlineData("ConsoleHost", new string[0], null, true)]
  [InlineData("ConsoleHost", new[] { "-NoProfile" }, null, true)]
  [InlineData("ConsoleHost", new[] { "-NonInteractive" }, null, false)]
  [InlineData("ConsoleHost", new[] { "-noni" }, null, false)]
  [InlineData("ConsoleHost", new[] { "-Command", "Get-Date" }, null, false)]
  [InlineData("ConsoleHost", new[] { "-c", "Get-Date", "-NoExit" }, null, true)]
  [InlineData("ConsoleHost", new[] { "-File", "x.ps1" }, null, false)]
  [InlineData("ConsoleHost", new[] { "-EncodedCommand", "AA==" }, null, false)]
  [InlineData("Default Host", new string[0], null, false)]
  [InlineData("Default Host", new string[0], "1", true)]
  [InlineData("ConsoleHost", new string[0], "0", false)]
  public void DetectionFollowsHostArgumentsAndOverride(string hostName, string[] arguments, string? overrideValue, bool expected)
    => InteractiveHost.Detect(hostName, arguments, overrideValue).ShouldBe(expected);
}
