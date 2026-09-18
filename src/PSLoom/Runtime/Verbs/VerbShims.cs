// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Concurrent;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   The command each verb shim invokes. Called from generated script text (<c>&amp; ([VerbShims]::Get(id)) @args</c>); a
///   <see cref="CmdletInfo" /> depends only on the verb type, so ids are process-wide.
/// </summary>
public static class VerbShims {
  private static readonly ConcurrentDictionary<int, CmdletInfo> _commands = new();
  private static readonly ConcurrentDictionary<Type, int> _ids = new();
  private static int _nextId;

  /// <summary>
  ///   Gets the command behind a shim id.
  /// </summary>
  /// <param name="id">The id baked into the shim.</param>
  /// <returns>The verb's command.</returns>
  public static CmdletInfo Get(int id)
    => _commands[id];

  internal static int Register(Type verbType, string name)
    => _ids.GetOrAdd(verbType, type => {
      var id = Interlocked.Increment(ref _nextId);
      _commands[id] = new CmdletInfo($"Weave-{name}", type);
      return id;
    });
}
