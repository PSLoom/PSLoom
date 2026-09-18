// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using System.Management.Automation.Language;
using PSLoom.Runtime.Loom;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   The draft prepass: finds the harnesses a draft threads before anything runs.
/// </summary>
internal static class DraftAnalyzer {
  internal const string THREAD_VERB = "Thread";

  /// <summary>
  ///   Collects the top-level <c>Thread</c> statements with literal names (and optional literal <c>-Version</c>), in order and
  ///   without duplicates, and an error for every <c>Thread</c> that is nested, non-literal, or conflicts on version.
  /// </summary>
  public static (IReadOnlyList<ThreadDeclaration> Threads, IReadOnlyList<LoomException> Errors) FindThreads(ScriptBlockAst draft) {
    ArgumentNullException.ThrowIfNull(draft);

    var topLevel = new HashSet<Ast>();

    foreach (var statement in (IEnumerable<StatementAst>?)draft.EndBlock?.Statements ?? []) {
      if (statement is PipelineAst { PipelineElements: [CommandAst command] } &&
          IsThread(command)) {
        topLevel.Add(command);
      }
    }

    var threads = new List<ThreadDeclaration>();
    var errors = new List<LoomException>();

    foreach (var ast in draft.FindAll(static node => node is CommandAst command && IsThread(command), true)) {
      var command = (CommandAst)ast;

      if (!topLevel.Contains(command)) {
        errors.Add(LoomException.ThreadNotTopLevel(command.Extent));
        continue;
      }

      if (Read(command, errors) is not { } declaration) {
        continue;
      }

      var existing = threads.Find(thread => string.Equals(thread.Name, declaration.Name, StringComparison.OrdinalIgnoreCase));

      if (existing is null) {
        threads.Add(declaration);
      }
      else if (existing.Version != declaration.Version) {
        errors.Add(LoomException.ThreadVersionConflict(declaration.Name, declaration.Extent));
      }
    }

    return (threads, errors);
  }

  private static bool IsThread(CommandAst command)
    => string.Equals(command.GetCommandName(), THREAD_VERB, StringComparison.OrdinalIgnoreCase);

  private static ThreadDeclaration? Read(CommandAst command, List<LoomException> errors) {
    var elements = command.CommandElements;
    Ast? nameAst = null;
    Ast? versionAst = null;
    var hasVersion = false;

    for (var index = 1; index < elements.Count; index++) {
      if (elements[index] is not CommandParameterAst parameter) {
        if (nameAst is not null) {
          errors.Add(LoomException.ThreadNameNotLiteral(command.Extent));
          return null;
        }

        nameAst = elements[index];
        continue;
      }

      var value = parameter.Argument ?? ((index + 1) < elements.Count ? elements[++index] : null);

      if ("Name".StartsWith(parameter.ParameterName, StringComparison.OrdinalIgnoreCase)) {
        nameAst = value;
      }
      else if ("Version".StartsWith(parameter.ParameterName, StringComparison.OrdinalIgnoreCase)) {
        versionAst = value;
        hasVersion = true;
      }
      else {
        errors.Add(LoomException.ThreadNameNotLiteral(command.Extent));
        return null;
      }
    }

    if (nameAst is not StringConstantExpressionAst { Value.Length: > 0 } name) {
      errors.Add(LoomException.ThreadNameNotLiteral(command.Extent));
      return null;
    }

    Version? version = null;

    if (!hasVersion ||
        TryReadVersion(versionAst, out version)) {
      return new ThreadDeclaration(name.Value, version, command.Extent);
    }

    errors.Add(LoomException.ThreadVersionNotLiteral(command.Extent));
    return null;
  }

  private static bool TryReadVersion(Ast? ast, out Version? version) {
    var text = ast switch {
      StringConstantExpressionAst constant => constant.Value,
      ConstantExpressionAst { Value: IFormattable number } => number.ToString(null, CultureInfo.InvariantCulture),
      var _ => null
    };

    version = null;
    return text is not null && Version.TryParse(text, out version);
  }
}
