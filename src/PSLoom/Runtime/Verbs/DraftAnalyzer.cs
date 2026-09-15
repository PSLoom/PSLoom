// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using PSLoom.Runtime.Loom;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   The draft prepass: finds the harnesses a draft threads before anything runs.
/// </summary>
internal static class DraftAnalyzer {
  internal const string THREAD_VERB = "Thread";

  /// <summary>
  ///   Collects the literal names of top-level <c>Thread</c> statements, in order and without duplicates, and an error for every
  ///   <c>Thread</c> that is nested or has a non-literal name.
  /// </summary>
  public static (IReadOnlyList<string> Harnesses, IReadOnlyList<LoomException> Errors) FindThreads(ScriptBlockAst draft) {
    ArgumentNullException.ThrowIfNull(draft);

    var topLevel = new HashSet<Ast>();

    foreach (var statement in draft.EndBlock?.Statements ?? []) {
      if (statement is PipelineAst { PipelineElements: [CommandAst command] } &&
          IsThread(command)) {
        topLevel.Add(command);
      }
    }

    var harnesses = new List<string>();
    var errors = new List<LoomException>();

    foreach (var ast in draft.FindAll(static node => node is CommandAst command && IsThread(command), true)) {
      var command = (CommandAst)ast;

      if (!topLevel.Contains(command)) {
        errors.Add(LoomException.ThreadNotTopLevel(command.Extent));
        continue;
      }

      if (LiteralName(command) is not { } name) {
        errors.Add(LoomException.ThreadNameNotLiteral(command.Extent));
        continue;
      }

      if (!harnesses.Contains(name, StringComparer.OrdinalIgnoreCase)) {
        harnesses.Add(name);
      }
    }

    return (harnesses, errors);
  }

  private static bool IsThread(CommandAst command)
    => string.Equals(command.GetCommandName(), THREAD_VERB, StringComparison.OrdinalIgnoreCase);

  private static string? LiteralName(CommandAst command) {
    var elements = command.CommandElements;

    if (elements.Count < 2) {
      return null;
    }

    if (elements[1] is not CommandParameterAst parameter) {
      return Literal(elements[1]);
    }

    return !"Name".StartsWith(parameter.ParameterName, StringComparison.OrdinalIgnoreCase)
      ? null
      : Literal(parameter.Argument ?? (elements.Count > 2 ? elements[2] : null));
  }

  private static string? Literal(Ast? ast)
    => ast is StringConstantExpressionAst { Value.Length: > 0 } constant ? constant.Value : null;
}
