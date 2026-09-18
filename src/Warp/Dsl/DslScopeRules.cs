// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;

namespace PSLoom.Warp.Dsl;

/// <summary>
///   Enforces that scope types are pure tokens. The kernel's <see cref="IVerbRegistry.Add{TVerb}" /> calls
///   <see cref="EnsureValidScopes" /> before registering a verb.
/// </summary>
internal static class DslScopeRules {
  private const BindingFlags INSTANCE_CONSTRUCTORS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

  /// <summary>
  ///   Validates every scope a verb references: each <see cref="LoomVerbAttribute.Scopes" /> entry and each
  ///   <see cref="OpensScopeAttribute.Scope" /> on its properties.
  /// </summary>
  /// <param name="verbType">The verb type.</param>
  /// <exception cref="WarpException">A referenced scope is invalid (<c>WARP_SCOPE_INVALID</c>).</exception>
  public static void EnsureValidScopes(Type verbType) {
    ArgumentNullException.ThrowIfNull(verbType);

    if (verbType.GetCustomAttribute<LoomVerbAttribute>(false) is { } verb) {
      foreach (var scope in verb.Scopes) {
        EnsureValid(scope, verbType);
      }
    }

    foreach (var property in verbType.GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
      if (property.GetCustomAttribute<OpensScopeAttribute>(false) is { } opens) {
        EnsureValid(opens.Scope, verbType);
      }
    }
  }

  /// <summary>
  ///   Validates one scope type.
  /// </summary>
  /// <param name="scopeType">The scope type.</param>
  /// <param name="owner">The type that referenced it, named in the error.</param>
  /// <exception cref="WarpException">The scope is invalid (<c>WARP_SCOPE_INVALID</c>).</exception>
  public static void EnsureValid(Type? scopeType, Type owner) {
    ArgumentNullException.ThrowIfNull(owner);

    if (scopeType is null) {
      throw WarpException.ScopeInvalid(typeof(DslScope), owner, "the scope type is null.");
    }

    if (!scopeType.IsSubclassOf(typeof(DslScope))) {
      throw WarpException.ScopeInvalid(scopeType, owner, $"it does not derive from {nameof(DslScope)}.");
    }

    if (scopeType.ContainsGenericParameters) {
      throw WarpException.ScopeInvalid(scopeType, owner, "open generic types cannot be scopes.");
    }

    if (scopeType.IsAbstract) {
      return;
    }

    if (scopeType.IsSealed &&
        scopeType.GetConstructors(INSTANCE_CONSTRUCTORS).All(constructor => constructor.IsPrivate)) {
      return;
    }

    throw WarpException.ScopeInvalid(scopeType, owner,
      $"it can be instantiated. Declare it as 'public abstract class {scopeType.Name} : {nameof(DslScope)};'.");
  }
}
