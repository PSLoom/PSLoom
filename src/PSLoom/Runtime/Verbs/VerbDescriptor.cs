// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   A registered verb: its attribute metadata, read once, plus what the shim and static validation need.
/// </summary>
internal sealed class VerbDescriptor {
  private VerbDescriptor(Type verbType, LoomVerbAttribute attribute, string owner, IReadOnlyList<VerbParameter> parameters) {
    VerbType = verbType;
    Name = attribute.Name;
    Owner = owner;
    Scopes = [.. attribute.Scopes];
    Reweave = attribute.Reweave;
    Parameters = parameters;
    ShimId = VerbShims.Register(verbType, attribute.Name);
    Shim = ScriptBlock.Create($"& ([PSLoom.Runtime.Verbs.VerbShims]::Get({ShimId})) @args");
  }

  public Type VerbType { get; }

  public string Name { get; }

  public string Owner { get; }

  public IReadOnlyList<Type> Scopes { get; }

  public ReweaveBehavior Reweave { get; }

  public IReadOnlyList<VerbParameter> Parameters { get; }

  public int ShimId { get; }

  /// <summary>
  ///   Gets the function body injected into a scope's verb table.
  /// </summary>
  public ScriptBlock Shim { get; }

  public static VerbDescriptor Create(Type verbType, LoomVerbAttribute attribute, string owner)
    => new(verbType, attribute, owner, ReadParameters(verbType));

  public bool IsValidIn(Type scope) {
    foreach (var candidate in Scopes) {
      if (candidate == scope) {
        return true;
      }
    }

    return false;
  }

  /// <summary>
  ///   Finds a parameter by exact name or alias, then by unique prefix, as PowerShell's binder does (case-insensitive).
  /// </summary>
  public VerbParameter? MatchParameter(string name) {
    VerbParameter? prefixMatch = null;
    var ambiguous = false;

    foreach (var parameter in Parameters) {
      if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase) ||
          parameter.Aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))) {
        return parameter;
      }

      if (!parameter.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)) {
        continue;
      }

      ambiguous |= prefixMatch is not null;
      prefixMatch = parameter;
    }

    return ambiguous ? null : prefixMatch;
  }

  private static VerbParameter[] ReadParameters(Type verbType) {
    var parameters = new List<VerbParameter>();

    foreach (var property in verbType.GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
      var parameterAttributes = property.GetCustomAttributes<ParameterAttribute>(true).ToArray();

      if (parameterAttributes.Length == 0) {
        continue;
      }

      var position = parameterAttributes
        .Select(attribute => attribute.Position)
        .Where(value => value >= 0)
        .Select(value => (int?)value)
        .FirstOrDefault();

      parameters.Add(new VerbParameter(
        property.Name,
        property.GetCustomAttribute<AliasAttribute>(true)?.AliasNames.ToArray() ?? [],
        position,
        property.PropertyType == typeof(SwitchParameter),
        property.GetCustomAttribute<OpensScopeAttribute>(false)?.Scope));
    }

    return [.. parameters];
  }
}
