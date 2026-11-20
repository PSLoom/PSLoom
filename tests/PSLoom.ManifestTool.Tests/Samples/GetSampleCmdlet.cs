// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;

namespace PSLoom.ManifestTool.Tests.Samples;

[Cmdlet(VerbsCommon.Get, "Sample")]
public sealed class GetSampleCmdlet : PSCmdlet;
