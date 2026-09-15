// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   Static scope validation: finds verbs used where their scope does not allow them, before a draft executes.
/// </summary>
internal sealed class DraftValidator {
  private readonly VerbRegistry _registry;
  private readonly Dictionary<CommandAst, Type> _scopes = [];

  private DraftValidator(VerbRegistry registry)
    => _registry = registry;

  /// <summary>
  ///   Validates every verb call in a draft whose root scope is <see cref="DraftScope" />.
  /// </summary>
  public static IReadOnlyList<LoomException> Validate(ScriptBlockAst draft, VerbRegistry registry)
    => Validate(draft, registry, typeof(DraftScope));

  /// <summary>
  ///   Validates every verb call in a body that runs in <paramref name="rootScope" />.
  /// </summary>
  public static IReadOnlyList<LoomException> Validate(ScriptBlockAst body, VerbRegistry registry, Type rootScope) {
    ArgumentNullException.ThrowIfNull(body);
    ArgumentNullException.ThrowIfNull(registry);

    var validator = new DraftValidator(registry);
    var errors = new List<LoomException>();

    foreach (var ast in body.FindAll(static node => node is CommandAst, true)) {
      var command = (CommandAst)ast;

      if (command.GetCommandName() is not { } name ||
          registry.FindByName(name) is not { Count: > 0 } candidates) {
        continue;
      }

      var scope = validator.ScopeOf(command, rootScope);

      if (!candidates.Any(candidate => candidate.IsValidIn(scope))) {
        errors.Add(LoomException.VerbOutOfScope(candidates[0].Name, scope, candidates.SelectMany(candidate => candidate.Scopes).Distinct(),
          command.Extent));
      }
    }

    return errors;
  }

  /// <summary>
  ///   Binds a verb call's arguments like PowerShell: named parameters by exact name, alias or unique prefix (switches take no
  ///   value), then the remaining elements by position.
  /// </summary>
  internal static Dictionary<Ast, VerbParameter> Bind(CommandAst command, VerbDescriptor descriptor) {
    var elements = command.CommandElements;
    var bound = new Dictionary<Ast, VerbParameter>();
    var named = new HashSet<VerbParameter>();
    var positional = new List<Ast>();

    for (var index = 1; index < elements.Count; index++) {
      if (elements[index] is not CommandParameterAst parameterAst) {
        positional.Add(elements[index]);
        continue;
      }

      if (descriptor.MatchParameter(parameterAst.ParameterName) is not { } parameter) {
        continue;
      }

      named.Add(parameter);

      if (parameterAst.Argument is { } argument) {
        bound[argument] = parameter;
      }
      else if (!parameter.IsSwitch &&
               index + 1 < elements.Count) {
        bound[elements[++index]] = parameter;
      }
    }

    using var byPosition = descriptor.Parameters
      .Where(parameter => parameter.Position is not null && !named.Contains(parameter))
      .OrderBy(parameter => parameter.Position)
      .GetEnumerator();

    foreach (var element in positional) {
      if (!byPosition.MoveNext()) {
        break;
      }

      bound[element] = byPosition.Current;
    }

    return bound;
  }

  private Type ScopeOf(CommandAst command, Type rootScope) {
    if (_scopes.TryGetValue(command, out var cached)) {
      return cached;
    }

    var scope = rootScope;

    for (Ast? child = command, parent = command.Parent; parent is not null; child = parent, parent = parent.Parent) {
      if (child is not ScriptBlockExpressionAst block ||
          OwnerOf(block) is not { } owner ||
          owner.GetCommandName() is not { } ownerName) {
        continue;
      }

      var ownerScope = ScopeOf(owner, rootScope);
      var descriptor = _registry.FindByName(ownerName).FirstOrDefault(candidate => candidate.IsValidIn(ownerScope));

      scope = descriptor is not null && Bind(owner, descriptor).TryGetValue(block, out var parameter) && parameter.OpensScope is { } opened
        ? opened
        : ownerScope;
      break;
    }

    _scopes[command] = scope;
    return scope;
  }

  private static CommandAst? OwnerOf(ScriptBlockExpressionAst block)
    => block.Parent switch {
      CommandAst command => command,
      CommandParameterAst { Parent: CommandAst command } => command,
      var _ => null
    };
}
