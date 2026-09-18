// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   The verbs of one runspace, indexed by scope, with per-scope function tables rebuilt only when the registry changes.
/// </summary>
internal sealed class VerbRegistry {
  private readonly Dictionary<Type, Dictionary<string, VerbDescriptor>> _byScope = [];
  private readonly Dictionary<Type, VerbDescriptor> _byType = [];
  private readonly Lock _lock = new();
  private readonly Dictionary<Type, (int Generation, Dictionary<string, ScriptBlock> Table)> _tables = [];
  private int _generation;

  /// <summary>
  ///   Gets a counter bumped on every change.
  /// </summary>
  public int Generation {
    get {
      lock (_lock) {
        return _generation;
      }
    }
  }

  /// <summary>
  ///   Registers a verb type for an owner. Registering the same type again does nothing.
  /// </summary>
  /// <exception cref="LoomException">The attribute is missing or invalid, or the name is taken in one of the scopes.</exception>
  public VerbDescriptor Add(Type verbType, string owner) {
    ArgumentNullException.ThrowIfNull(verbType);
    ArgumentException.ThrowIfNullOrWhiteSpace(owner);

    lock (_lock) {
      if (_byType.TryGetValue(verbType, out var existing)) {
        return existing;
      }
    }

    var attribute = verbType.GetCustomAttribute<LoomVerbAttribute>(false) ?? throw LoomException.VerbAttributeMissing(verbType);

    if (!Identifiers.IsValid(attribute.Name)) {
      throw LoomException.VerbNameInvalid(attribute.Name, verbType);
    }

    if (attribute.Scopes.Count == 0) {
      throw LoomException.VerbNoScope(verbType);
    }

    DslScopeRules.EnsureValidScopes(verbType);
    var descriptor = VerbDescriptor.Create(verbType, attribute, owner);

    lock (_lock) {
      if (_byType.TryGetValue(verbType, out var raced)) {
        return raced;
      }

      foreach (var scope in descriptor.Scopes) {
        if (_byScope.TryGetValue(scope, out var verbs) &&
            verbs.TryGetValue(descriptor.Name, out var taken)) {
          throw LoomException.VerbDuplicate(descriptor.Name, scope, taken.Owner, owner);
        }
      }

      foreach (var scope in descriptor.Scopes) {
        if (!_byScope.TryGetValue(scope, out var verbs)) {
          _byScope[scope] = verbs = new Dictionary<string, VerbDescriptor>(StringComparer.OrdinalIgnoreCase);
        }

        verbs[descriptor.Name] = descriptor;
      }

      _byType[verbType] = descriptor;
      _generation++;
      return descriptor;
    }
  }

  /// <summary>
  ///   Removes every verb registered by an owner.
  /// </summary>
  public void RemoveOwner(string owner) {
    lock (_lock) {
      var removed = _byType.Values.Where(descriptor => string.Equals(descriptor.Owner, owner, StringComparison.Ordinal)).ToArray();

      if (removed.Length == 0) {
        return;
      }

      foreach (var descriptor in removed) {
        _byType.Remove(descriptor.VerbType);

        foreach (var scope in descriptor.Scopes) {
          _byScope[scope].Remove(descriptor.Name);
        }
      }

      _generation++;
    }
  }

  /// <summary>
  ///   Gets the descriptor of a registered verb type.
  /// </summary>
  public VerbDescriptor? ForType(Type verbType) {
    lock (_lock) {
      return _byType.GetValueOrDefault(verbType);
    }
  }

  /// <summary>
  ///   Gets every verb with a name, across scopes.
  /// </summary>
  public IReadOnlyList<VerbDescriptor> FindByName(string name) {
    lock (_lock) {
      return [
        .. _byType.Values.Where(descriptor => string.Equals(descriptor.Name, name, StringComparison.OrdinalIgnoreCase))
      ];
    }
  }

  /// <summary>
  ///   Gets every registered verb.
  /// </summary>
  public IReadOnlyList<VerbDescriptor> All() {
    lock (_lock) {
      return [.. _byType.Values];
    }
  }

  /// <summary>
  ///   Gets the functions to define when a body runs in a scope. The dictionary is shared: callers must not modify it.
  /// </summary>
  public Dictionary<string, ScriptBlock> TableFor(Type scope) {
    lock (_lock) {
      if (_tables.TryGetValue(scope, out var cached) &&
          cached.Generation == _generation) {
        return cached.Table;
      }

      var table = new Dictionary<string, ScriptBlock>(StringComparer.OrdinalIgnoreCase);

      if (_byScope.TryGetValue(scope, out var verbs)) {
        foreach (var (name, descriptor) in verbs) {
          table[name] = descriptor.Shim;
        }
      }

      _tables[scope] = (_generation, table);
      return table;
    }
  }
}
