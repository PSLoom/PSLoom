// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using System.Text;

namespace PSLoom.Runtime.Treadles;

/// <summary>
///   A parsed treadle body: one command plus the constant arguments baked before the caller's own. Parsing happens once, when the
///   treadle is created, so an invocation never pays for it.
/// </summary>
internal sealed class TreadleBody {
  private readonly IReadOnlyList<TreadleToken> _tokens;

  private TreadleBody(string targetCommand, IReadOnlyList<TreadleToken> tokens) {
    TargetCommand = targetCommand;
    _tokens = tokens;
    BakedTokens = [.. tokens.Select(token => token.Text)];
  }

  /// <summary>
  ///   Gets the command the treadle invokes.
  /// </summary>
  public string TargetCommand { get; }

  /// <summary>
  ///   Gets the constant tokens placed before the caller's arguments, as written.
  /// </summary>
  public IReadOnlyList<string> BakedTokens { get; }

  /// <summary>
  ///   Parses a body written as a script block or as command text.
  /// </summary>
  /// <param name="command">A <see cref="ScriptBlock" /> or a string; anything else is converted with <see cref="object.ToString" />.</param>
  /// <exception cref="TreadleException">The body is not a single command with constant arguments.</exception>
  public static TreadleBody Parse(object command) {
    ArgumentNullException.ThrowIfNull(command);

    if (command is PSObject { BaseObject: { } unwrapped }) {
      command = unwrapped;
    }

    if (command is ScriptBlock scriptBlock) {
      return Parse(scriptBlock);
    }

    var ast = Parser.ParseInput(command.ToString()?.Trim() ?? string.Empty, out var _, out var errors);

    return errors.Length > 0
      ? throw TreadleException.BodyNotSingleCommand(errors[0].Message, errors[0].Extent)
      : Parse(ast);
  }

  /// <summary>
  ///   Parses a body written as a script block.
  /// </summary>
  /// <exception cref="TreadleException">The body is not a single command with constant arguments.</exception>
  public static TreadleBody Parse(ScriptBlock scriptBlock) {
    ArgumentNullException.ThrowIfNull(scriptBlock);
    return Parse((ScriptBlockAst)scriptBlock.Ast);
  }

  /// <summary>
  ///   Renders the equivalent wrapper, for example <c>&amp; 'Get-ChildItem' -Force 'src' @args</c>. Values are quoted; parameters keep
  ///   their spelling so the binder still sees them as parameters.
  /// </summary>
  public string ToWrapperScript() {
    var builder = new StringBuilder("& ");
    Quote(builder, TargetCommand);

    foreach (var token in _tokens) {
      builder.Append(' ');

      if (token.IsParameter) {
        builder.Append(token.Text);
        continue;
      }

      Quote(builder, token.Text);
    }

    return builder.Append(" @args").ToString();
  }

  /// <inheritdoc />
  public override string ToString()
    => BakedTokens.Count == 0 ? TargetCommand : $"{TargetCommand} {string.Join(' ', BakedTokens)}";

  private static TreadleBody Parse(ScriptBlockAst ast) {
    if (ast.ParamBlock is not null) {
      throw TreadleException.BodyNotSingleCommand("it declares parameters", ast.ParamBlock.Extent);
    }

    if (ast.BeginBlock is not null ||
        ast.ProcessBlock is not null ||
        ast.CleanBlock is not null ||
        ast.DynamicParamBlock is not null) {
      throw TreadleException.BodyNotSingleCommand("it declares named blocks", ast.Extent);
    }

    var statements = ast.EndBlock?.Statements ?? [];

    if (statements.Count != 1) {
      throw TreadleException.BodyNotSingleCommand(statements.Count == 0 ? "it is empty" : "it holds more than one statement", ast.Extent);
    }

    if (statements[0] is not PipelineAst { PipelineElements: [CommandAst command] }) {
      throw TreadleException.BodyNotSingleCommand("it is not a single command", statements[0].Extent);
    }

    if (command.Redirections.Count > 0) {
      throw TreadleException.BodyNotSingleCommand("it redirects output", command.Redirections[0].Extent);
    }

    if (command.InvocationOperator is not (TokenKind.Unknown or TokenKind.Ampersand)) {
      throw TreadleException.BodyNotSingleCommand("it uses an invocation operator other than '&'", command.Extent);
    }

    if (command.CommandElements[0] is not StringConstantExpressionAst { Value: { Length: > 0 } target }) {
      throw TreadleException.BodyNotSingleCommand("the command name is not a literal", command.CommandElements[0].Extent);
    }

    var tokens = new List<TreadleToken>(command.CommandElements.Count - 1);

    foreach (var element in command.CommandElements.Skip(1)) {
      tokens.Add(Token(element));
    }

    return new TreadleBody(target, tokens);
  }

  private static TreadleToken Token(CommandElementAst element)
    => element switch {
      // A parameter keeps its original spelling, so '--oneline', '-Force' and '-p:value' survive verbatim.
      CommandParameterAst parameter => new TreadleToken(parameter.Extent.Text, true),
      StringConstantExpressionAst text => new TreadleToken(text.Value, false),
      ConstantExpressionAst { Value: { } value } => new TreadleToken(LanguagePrimitives.ConvertTo<string>(value), false),
      var _ => throw TreadleException.BodyNotSingleCommand("an argument is not a constant", element.Extent)
    };

  private static void Quote(StringBuilder builder, string token)
    => builder.Append('\'').Append(token.Replace("'", "''", StringComparison.Ordinal)).Append('\'');

  private readonly record struct TreadleToken(string Text, bool IsParameter);
}
