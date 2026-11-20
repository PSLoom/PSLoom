// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;

namespace PSLoom.ManifestTool.Tests.Samples;

[Cmdlet("Invoke", "Sample")]
[SuppressMessage("ReSharper", "UnusedType.Global",
  Justification = "Found by the scanner through the assembly metadata, never referenced from code.")]
public sealed class InvokeSampleCmdlet : PSCmdlet;
