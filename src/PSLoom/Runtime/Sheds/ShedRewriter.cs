// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using System.Text;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   Rewrites a draft so staged statements do not run in place. Every replacement keeps the number of lines of what it replaces and
///   the text is re-parsed with the draft's file name, so line numbers, error positions and <c>$PSScriptRoot</c> are unchanged.
/// </summary>
internal static class ShedRewriter {
  private const string BRIDGE = "[PSLoom.Runtime.Sheds.ShedBridge]";

  public static ScriptBlock Rewrite(ScriptBlock draft, IReadOnlyList<ShedDeclaration> declarations) {
    ArgumentNullException.ThrowIfNull(draft);
    ArgumentNullException.ThrowIfNull(declarations);

    if (declarations.Count == 0) {
      return draft;
    }

    var body = (ScriptBlockAst)draft.Ast;
    var origin = body.Extent.StartOffset;
    var text = new StringBuilder(body.Extent.Text);

    // Replace from the end, so earlier offsets stay valid.
    foreach (var declaration in declarations.OrderByDescending(declaration => declaration.Statement.Extent.StartOffset)) {
      var statement = declaration.Statement.Extent;
      // Apply-now statements are dot-sourced, so they keep seeing the draft's variables; -Lucid assigns their output away.
      var run = declaration.Lucid ? $"$null = . {{ {statement.Text} }}" : $". {{ {statement.Text} }}";
      // A failure that escapes the statement (a parameter binding error, a throw) is recorded against the entry instead of ending the
      // draft: a staged statement is isolated whether it applies now or later.
      var replacement = declaration.Timing == ShedTiming.Now
        ? $"if ({BRIDGE}::Admit({declaration.Index})) {{ try {{ {run}; {BRIDGE}::Applied({declaration.Index}) }} " +
          $"catch {{ {BRIDGE}::Failed({declaration.Index}, $_) }} }}"
        : $"{BRIDGE}::Capture({declaration.Index})" + Blank(statement.Text);

      Replace(text, statement.StartOffset - origin, statement.Text.Length, replacement);
      Replace(text, declaration.ShedStatement.Extent.StartOffset - origin, declaration.ShedStatement.Extent.Text.Length,
        Blank(declaration.ShedStatement.Extent.Text, keepColumns: true));
    }

    return Parse(Unwrap(text.ToString(), body.Extent), draft.File);
  }

  /// <summary>
  ///   The staged statement alone, placed on its original lines, for applying after the draft.
  /// </summary>
  public static ScriptBlock StatementScript(ShedDeclaration declaration, string? file) {
    ArgumentNullException.ThrowIfNull(declaration);

    var extent = declaration.Statement.Extent;
    return Parse(new string('\n', extent.StartLineNumber - 1) + new string(' ', extent.StartColumnNumber - 1) + extent.Text, file);
  }

  private static void Replace(StringBuilder text, int start, int length, string replacement)
    => text.Remove(start, length).Insert(start, replacement);

  /// <summary>
  ///   Spaces for every character but line breaks, so what follows keeps its line (and, with <paramref name="keepColumns" />, column).
  /// </summary>
  private static string Blank(string text, bool keepColumns = false) {
    var builder = new StringBuilder();

    foreach (var character in text) {
      if (character is '\n' or '\r') {
        builder.Append(character);
      }
      else if (keepColumns) {
        builder.Append(' ');
      }
    }

    return keepColumns ? builder.ToString() : builder.ToString().Replace("\r", string.Empty, StringComparison.Ordinal);
  }

  /// <summary>
  ///   Drops the braces of a script block literal and pads it back to its original line and column.
  /// </summary>
  private static string Unwrap(string text, IScriptExtent extent) {
    var isLiteral = text.StartsWith('{') && text.EndsWith('}');
    var inner = isLiteral ? " " + text[1..^1] : text;

    return new string('\n', extent.StartLineNumber - 1) + new string(' ', extent.StartColumnNumber - 1) + inner;
  }

  private static ScriptBlock Parse(string text, string? file) {
    var ast = Parser.ParseInput(text, file, out _, out var errors);

    // The input came from a draft that already parsed, and every replacement is well-formed, so this can only be a bug here.
    return errors.Length == 0
      ? ast.GetScriptBlock()
      : throw new InvalidOperationException($"PSLoom rewrote a draft it cannot parse: {errors[0].Message}");
  }
}
