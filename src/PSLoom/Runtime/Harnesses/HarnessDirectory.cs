// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using PSLoom.Runtime.Loom;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   APIs published by harnesses in one runspace, keyed by interface type.
/// </summary>
internal sealed class HarnessDirectory {
  private readonly Dictionary<Type, (object Api, string Owner)> _apis = [];
  private readonly Lock _lock = new();

  /// <exception cref="LoomException">Another owner already published <typeparamref name="TApi" />.</exception>
  public void Publish<TApi>(TApi api, string owner) where TApi : class {
    ArgumentNullException.ThrowIfNull(api);

    lock (_lock) {
      if (_apis.TryGetValue(typeof(TApi), out var existing) &&
          !string.Equals(existing.Owner, owner, StringComparison.Ordinal)) {
        throw LoomException.ApiDuplicate(typeof(TApi), existing.Owner, owner);
      }

      _apis[typeof(TApi)] = (api, owner);
    }
  }

  public bool TryGet<TApi>([NotNullWhen(true)] out TApi? api) where TApi : class {
    lock (_lock) {
      api = _apis.TryGetValue(typeof(TApi), out var entry) ? (TApi)entry.Api : null;
      return api is not null;
    }
  }

  public void RemoveOwner(string owner) {
    lock (_lock) {
      foreach (var type in _apis.Where(pair => string.Equals(pair.Value.Owner, owner, StringComparison.Ordinal)).Select(pair => pair.Key).ToArray()) {
        _apis.Remove(type);
      }
    }
  }
}
