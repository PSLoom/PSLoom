// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.TestKit;

/// <summary>The outcome of a pwsh process.</summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="StandardOutput">Everything written to standard output.</param>
/// <param name="StandardError">Everything written to standard error.</param>
public sealed record PwshResult(int ExitCode, string StandardOutput, string StandardError);
