// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Verbs;
using PSLoom.Warp.Kernel;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   The harnesses composed in one runspace.
/// </summary>
internal sealed class HarnessRegistry(LoomSession session) {
  private readonly Dictionary<string, HarnessServices> _byName = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<Type, HarnessServices> _byType = [];
  private readonly Lock _lock = new();

  /// <summary>
  ///   Gets every composed harness.
  /// </summary>
  public IReadOnlyList<HarnessServices> All {
    get {
      lock (_lock) {
        return [.. _byType.Values];
      }
    }
  }

  /// <summary>
  ///   Composes a harness for this runspace. A type already composed here is not composed again.
  /// </summary>
  /// <exception cref="LoomException">The name is invalid or taken, or composition failed (its verbs and APIs are rolled back).</exception>
  public HarnessServices Register(HarnessRegistration registration) {
    ArgumentNullException.ThrowIfNull(registration);

    var name = registration.Attribute.Name;

    if (!Identifiers.IsValid(name)) {
      throw LoomException.HarnessNameInvalid(name, registration.HarnessType);
    }

    HarnessServices services;

    lock (_lock) {
      if (_byType.TryGetValue(registration.HarnessType, out var existing)) {
        return existing;
      }

      if (_byName.TryGetValue(name, out var taken)) {
        throw LoomException.HarnessDuplicate(name, taken.HarnessType, registration.HarnessType);
      }

      services = new HarnessServices(session, registration);
      _byType[registration.HarnessType] = services;
      _byName[name] = services;
    }

    try {
      registration.Factory().Compose(services);
      return services;
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      lock (_lock) {
        _byType.Remove(registration.HarnessType);
        _byName.Remove(name);
      }

      session.Verbs.RemoveOwner(name);
      session.Directory.RemoveOwner(name);
      throw LoomException.ComposeFailed(name, exception);
    }
  }

  public bool TryGetByName(string name, [NotNullWhen(true)] out HarnessServices? services) {
    lock (_lock) {
      return _byName.TryGetValue(name, out services);
    }
  }

  /// <exception cref="LoomException">The harness is not composed in this runspace.</exception>
  public HarnessServices GetContext(Type harnessType) {
    lock (_lock) {
      return _byType.TryGetValue(harnessType, out var services) ? services : throw LoomException.HarnessNotComposed(harnessType);
    }
  }
}
