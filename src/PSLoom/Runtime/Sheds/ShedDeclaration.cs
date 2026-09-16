// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   What the prepass read from one <c>Shed</c> and the statement it stages.
/// </summary>
internal sealed record ShedDeclaration(
  int Index,
  StatementAst ShedStatement,
  StatementAst Statement,
  ShedTiming Timing,
  string Slot,
  ScriptBlock? LoadIf,
  string? RequiresCommand,
  bool Lucid,
  bool Silent,
  ScriptBlock? AtLoad,
  bool IsVerb
);
