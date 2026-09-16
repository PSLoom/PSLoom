// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using PSLoom.Warp.Treadles;

namespace PSLoom.Runtime.Treadles;

/// <summary>
///   One runspace's treadles. A treadle is a global function equivalent to <c>&amp; &lt;command&gt; &lt;baked&gt; @args</c>; the
///   definitions live in memory only, like styles, and are re-declared every session.
/// </summary>
internal sealed class TreadleCatalog : ITreadleCatalog {
  // Defining is scope-qualified so the function is global whatever scope the cmdlet ran in; removal is not, because the provider
  // silently ignores a 'global:' qualifier on RemoveItem and would leave the function behind.
  private const string FUNCTION_PATH_PREFIX = "function:global:";
  private const string FUNCTION_REMOVE_PREFIX = "function:";
  private const string ALIAS_PATH_PREFIX = "alias:";
  private static readonly SearchValues<char> _invalidNameCharacters = SearchValues.Create("*?[]/\\:;,|&<>(){}$@#`\"' \t");

  private readonly Dictionary<string, TreadleDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>
  ///   Gets the catalog of each runspace.
  /// </summary>
  public static RunspaceLocal<TreadleCatalog> PerRunspace { get; } = new(static _ => new TreadleCatalog());

  /// <inheritdoc />
  public IReadOnlyCollection<TreadleDefinition> All => [.. _definitions.Values];

  /// <inheritdoc />
  public event EventHandler<TreadleChangedEventArgs>? Changed;

  /// <inheritdoc />
  public bool TryGet(string name, [NotNullWhen(true)] out TreadleDefinition? definition) {
    ArgumentNullException.ThrowIfNull(name);
    return _definitions.TryGetValue(name, out definition);
  }

  /// <summary>
  ///   Defines or redefines a treadle and installs its function.
  /// </summary>
  /// <param name="engine">The engine intrinsics of the session to define the function in.</param>
  /// <param name="name">The treadle name.</param>
  /// <param name="body">The parsed body.</param>
  /// <param name="force">Replaces a command this catalog does not own.</param>
  /// <returns>What the write did, including any subscriber failure the caller reports.</returns>
  /// <exception cref="TreadleException">
  ///   The name is unusable (<c>TREADLE_INVALID_NAME</c>) or already taken (<c>TREADLE_NAME_COLLISION</c>).
  /// </exception>
  public TreadleWriteOutcome Set(EngineIntrinsics engine, string name, TreadleBody body, bool force) {
    ArgumentNullException.ThrowIfNull(engine);
    ArgumentNullException.ThrowIfNull(body);

    if (string.IsNullOrWhiteSpace(name) ||
        name.AsSpan().ContainsAny(_invalidNameCharacters)) {
      throw TreadleException.InvalidName(name);
    }

    var previous = _definitions.GetValueOrDefault(name);

    if (previous is null &&
        !force &&
        Collision(engine, name) is { } existing) {
      throw TreadleException.NameCollision(name, existing);
    }

    var definition = new TreadleDefinition(name, body.TargetCommand, body.BakedTokens);
    engine.SessionState.InvokeProvider.Item.Set(FUNCTION_PATH_PREFIX + name, ScriptBlock.Create(body.ToWrapperScript()));
    _definitions[name] = definition;

    return new TreadleWriteOutcome(definition, previous, Raise(name, previous, definition));
  }

  /// <summary>
  ///   Removes a treadle and the function it installed. A same-named command this catalog does not own is left alone.
  /// </summary>
  /// <returns>What the removal did, or <see langword="null" /> when no treadle had that name.</returns>
  public TreadleWriteOutcome? Remove(EngineIntrinsics engine, string name) {
    ArgumentNullException.ThrowIfNull(engine);
    ArgumentNullException.ThrowIfNull(name);

    if (!_definitions.Remove(name, out var previous)) {
      return null;
    }

    var path = FUNCTION_REMOVE_PREFIX + previous.Name;

    if (engine.SessionState.InvokeProvider.Item.Exists(path)) {
      engine.SessionState.InvokeProvider.Item.Remove(path, false);
    }

    return new TreadleWriteOutcome(null, previous, Raise(previous.Name, previous, null));
  }

  /// <summary>
  ///   Describes the command already holding the name, or <see langword="null" /> when it is free. Shadowing a program is what a
  ///   treadle is for, so applications are not collisions; every lookup here is a table read, never command discovery, which would
  ///   scan every module path for a name that is usually free.
  /// </summary>
  private static string? Collision(EngineIntrinsics engine, string name) {
    var provider = engine.SessionState.InvokeProvider;

    if (provider.Item.Exists(FUNCTION_REMOVE_PREFIX + name)) {
      return "a function";
    }

    if (!provider.Item.Exists(ALIAS_PATH_PREFIX + name)) {
      return engine.InvokeCommand.GetCmdlet(name) is { } cmdlet ? $"a cmdlet from '{cmdlet.ModuleName}'" : null;
    }

    return provider.Item.Get(ALIAS_PATH_PREFIX + name).FirstOrDefault()?.BaseObject is not AliasInfo alias
      ? "an alias"
      : $"an alias for '{alias.Definition}'";
  }

  private IReadOnlyList<Exception> Raise(string name, TreadleDefinition? previous, TreadleDefinition? current) {
    if (Changed is not { } handlers) {
      return [];
    }

    List<Exception>? failures = null;
    var arguments = new TreadleChangedEventArgs(name, previous, current);

    // Every subscriber runs, like style watchers: one harness failing never hides another's work, nor the write itself.
    foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<TreadleChangedEventArgs>>()) {
      try {
        handler(this, arguments);
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
        (failures ??= []).Add(exception);
      }
    }

    return failures ?? (IReadOnlyList<Exception>)[];
  }
}
