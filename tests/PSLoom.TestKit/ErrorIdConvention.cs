// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using System.Text.RegularExpressions;

namespace PSLoom.TestKit;

/// <summary>
///   The error id convention: every id an exception type declares is SCREAMING_SNAKE with an owner prefix, and the prefix belongs
///   to the assembly declaring it. Ids are found as the literal string constants of <c>PowerShellException</c> subclasses.
/// </summary>
public static partial class ErrorIdConvention {
  private const string BASE_TYPE_NAME = "PowerShellException";

  /// <summary>
  ///   Gets every error id an assembly declares, with the type declaring it.
  /// </summary>
  public static IReadOnlyList<(Type Owner, string Id)> Ids(Assembly assembly) {
    ArgumentNullException.ThrowIfNull(assembly);

    return [
      .. assembly.GetTypes()
        .Where(IsPowerShellException)
        .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
          .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
          .Select(field => (type, (string)field.GetRawConstantValue()!)))
    ];
  }

  /// <summary>
  ///   Describes every id that breaks the convention for an assembly owning the given prefixes.
  /// </summary>
  /// <param name="assembly">The assembly to check.</param>
  /// <param name="ownedPrefixes">The prefixes this assembly may use, without the underscore (<c>LOOM</c>, <c>STYLE</c>).</param>
  /// <returns>One line per violation; empty when the assembly follows the convention.</returns>
  public static IReadOnlyList<string> Violations(Assembly assembly, params string[] ownedPrefixes) {
    ArgumentNullException.ThrowIfNull(ownedPrefixes);

    var violations = new List<string>();

    foreach (var (owner, id) in Ids(assembly)) {
      if (!IdPattern().IsMatch(id)) {
        violations.Add($"{owner.Name}: '{id}' is not SCREAMING_SNAKE with a known owner prefix.");
        continue;
      }

      var prefix = id[..id.IndexOf('_', StringComparison.Ordinal)];

      if (!ownedPrefixes.Contains(prefix, StringComparer.Ordinal)) {
        violations.Add($"{owner.Name}: '{id}' uses the {prefix}_ prefix, which {assembly.GetName().Name} does not own.");
      }
    }

    return violations;
  }

  private static bool IsPowerShellException(Type type) {
    for (var current = type.BaseType; current is not null; current = current.BaseType) {
      if (current.Name == BASE_TYPE_NAME) {
        return true;
      }
    }

    return false;
  }

  [GeneratedRegex("^(WARP|LOOM|STYLE|HOOK|TREADLE|CREEL|REED)_[A-Z0-9]+(_[A-Z0-9]+)*$")]
  private static partial Regex IdPattern();
}
