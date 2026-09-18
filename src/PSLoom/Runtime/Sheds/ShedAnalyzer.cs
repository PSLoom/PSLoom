// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Verbs;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   Reads every <c>Shed</c> of a draft and the statement it stages, enforcing the static rules. Runs with the harnesses already
///   provisioned, so it knows which statements are verbs.
/// </summary>
internal static partial class ShedAnalyzer {
  internal const string SHED_VERB = "Shed";
  internal const string DEFAULT_SLOT = "0a";
  internal static readonly string[] matchArray = new[] { "Wait", "Slot", "OnDemand", "LoadIf", "RequiresCommand", "Lucid", "Silent", "AtLoad" };

  public static (IReadOnlyList<ShedDeclaration> Declarations, IReadOnlyList<LoomException> Errors) Analyze(ScriptBlockAst draft,
  VerbRegistry registry) {
    ArgumentNullException.ThrowIfNull(draft);
    ArgumentNullException.ThrowIfNull(registry);

    var statements = (IReadOnlyList<StatementAst>?)draft.EndBlock?.Statements ?? [];
    var declarations = new List<ShedDeclaration>();
    var errors = new List<LoomException>();
    var topLevel = new HashSet<Ast>();

    for (var index = 0; index < statements.Count; index++) {
      if (ShedCommand(statements[index]) is not { } shed) {
        continue;
      }

      topLevel.Add(shed);

      if ((index + 1) >= statements.Count) {
        errors.Add(LoomException.ShedDangling(shed.Extent));
        continue;
      }

      var next = statements[index + 1];

      if (ShedCommand(next) is not null) {
        errors.Add(LoomException.ShedStacked(next.Extent));
        continue;
      }

      if (Read(declarations.Count, statements[index], shed, next, registry, errors) is { } declaration) {
        declarations.Add(declaration);
      }
    }

    foreach (var ast in draft.FindAll(static node => node is CommandAst command && IsShed(command), true)) {
      if (!topLevel.Contains(ast)) {
        errors.Add(LoomException.ShedNotTopLevel(ast.Extent));
      }
    }

    return (declarations, errors);
  }

  private static ShedDeclaration? Read(int index, StatementAst shedStatement, CommandAst shed, StatementAst next, VerbRegistry registry,
  List<LoomException> errors) {
    var errorsBefore = errors.Count;
    var wait = false;
    var onDemand = false;
    string? slot = null;
    ScriptBlock? loadIf = null;
    ScriptBlock? atLoad = null;
    string? requiresCommand = null;
    var lucid = false;
    var silent = false;
    var elements = shed.CommandElements;

    for (var position = 1; position < elements.Count; position++) {
      if (elements[position] is not CommandParameterAst parameter) {
        errors.Add(LoomException.ShedUnknownModifier(elements[position].Extent.Text, elements[position].Extent));
        continue;
      }

      var name = parameter.ParameterName;
      var value = parameter.Argument;

      switch (Match(name)) {
        case "Wait":
          wait = true;
          break;
        case "OnDemand":
          onDemand = true;
          break;
        case "Lucid":
          lucid = true;
          break;
        case "Silent":
          silent = true;
          break;
        case "Slot":
          if (TakeValue() is StringConstantExpressionAst { Value: var text } &&
              SlotPattern().IsMatch(text)) {
            slot = text.ToLowerInvariant();
          }
          else {
            errors.Add(LoomException.ShedNotLiteral("Slot", "a literal slot such as '0a' or '1b'", shed.Extent));
          }

          break;
        case "RequiresCommand":
          if (TakeValue() is StringConstantExpressionAst { Value.Length: > 0 } command) {
            requiresCommand = command.Value;
          }
          else {
            errors.Add(LoomException.ShedNotLiteral("RequiresCommand", "a literal command name", shed.Extent));
          }

          break;
        case "LoadIf":
          if (TakeValue() is ScriptBlockExpressionAst condition) {
            loadIf = condition.ScriptBlock.GetScriptBlock();
          }
          else {
            errors.Add(LoomException.ShedNotLiteral("LoadIf", "a script block", shed.Extent));
          }

          break;
        case "AtLoad":
          if (TakeValue() is ScriptBlockExpressionAst action) {
            atLoad = action.ScriptBlock.GetScriptBlock();
          }
          else {
            errors.Add(LoomException.ShedNotLiteral("AtLoad", "a script block", shed.Extent));
          }

          break;
        default:
          errors.Add(LoomException.ShedUnknownModifier(name, parameter.Extent));
          break;
      }

      continue;

      Ast? TakeValue() {
        if (value is not null) {
          return value;
        }

        return (position + 1) < elements.Count && elements[position + 1] is not CommandParameterAst ? elements[++position] : null;
      }
    }

    if (((wait ? 1 : 0) + (slot is null ? 0 : 1) + (onDemand ? 1 : 0)) > 1) {
      errors.Add(LoomException.ShedTimingConflict(shed.Extent));
    }
    else if (onDemand) {
      // Cycle A: no verb implements the on-demand contract yet, so every -OnDemand is rejected here.
      errors.Add(LoomException.ShedNotOnDemand(next.Extent));
    }

    var commandName = (next as PipelineAst)?.PipelineElements is [CommandAst nextCommand] ? nextCommand.GetCommandName() : null;

    if (string.Equals(commandName, DraftAnalyzer.THREAD_VERB, StringComparison.OrdinalIgnoreCase)) {
      errors.Add(LoomException.ShedNotApplicable("'Thread' loads its harness before the draft runs.", next.Extent));
    }

    if (errors.Count > errorsBefore) {
      return null;
    }

    var timing = slot is not null ? ShedTiming.Slot : wait ? ShedTiming.Wait : ShedTiming.Now;
    var isVerb = commandName is not null && registry.FindByName(commandName).Count > 0;

    return new ShedDeclaration(index, shedStatement, next, timing, slot ?? DEFAULT_SLOT, loadIf, requiresCommand, lucid, silent, atLoad, isVerb);
  }

  private static CommandAst? ShedCommand(StatementAst statement)
    => statement is PipelineAst { PipelineElements: [CommandAst command] } && IsShed(command) ? command : null;

  private static bool IsShed(CommandAst command)
    => string.Equals(command.GetCommandName(), SHED_VERB, StringComparison.OrdinalIgnoreCase);

  private static string? Match(string parameterName)
    => matchArray.Where(name => name.StartsWith(parameterName, StringComparison.OrdinalIgnoreCase)).ToArray() is [var single]
      ? single
      : null;

  [GeneratedRegex("^[0-9]+[a-cA-C]$")]
  private static partial Regex SlotPattern();
}
