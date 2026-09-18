// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Loom;
using PSLoom.Runtime.PSReadLine;
using PSLoom.Warp.Hosting;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   A PSReadLine probe over the session's engine. Reports nothing loaded until the session has engine intrinsics.
/// </summary>
internal sealed class SessionPSReadLineProbe(LoomSession session) : IPSReadLineProbe {
  /// <inheritdoc />
  public bool IsLoaded => session.Engine is { } engine && new PSReadLineProbe(engine.InvokeCommand).IsLoaded;

  /// <inheritdoc />
  public bool HasOptionParameter(string parameterName)
    => session.Engine is { } engine && new PSReadLineProbe(engine.InvokeCommand).HasOptionParameter(parameterName);
}
