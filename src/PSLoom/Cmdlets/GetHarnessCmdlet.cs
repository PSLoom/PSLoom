// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;

namespace PSLoom.Cmdlets;

/// <summary>Lists the harnesses registered in the current process.</summary>
[Cmdlet(VerbsCommon.Get, "Harness")]
[OutputType(typeof(HarnessInfo))]
public sealed class GetHarnessCmdlet : PSCmdlet {
  /// <inheritdoc />
  protected override void ProcessRecord()
    => WriteObject(HarnessRegistry.GetRegistered(), true);
}
