// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using PSLoom.Warp;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   Raised by the loom host, the verb registry and the harness registry.
/// </summary>
public sealed class LoomException : PowerShellException {
  internal const string HARNESS_NAME_INVALID = "LOOM_HARNESS_NAME_INVALID";
  internal const string HARNESS_DUPLICATE = "LOOM_HARNESS_DUPLICATE";
  internal const string HARNESS_COMPOSE_FAILED = "LOOM_HARNESS_COMPOSE_FAILED";
  internal const string HARNESS_NOT_COMPOSED = "LOOM_HARNESS_NOT_COMPOSED";
  internal const string HARNESS_NOT_INSTALLED = "LOOM_HARNESS_NOT_INSTALLED";
  internal const string HARNESS_IMPORT_FAILED = "LOOM_HARNESS_IMPORT_FAILED";
  internal const string NOT_A_HARNESS = "LOOM_NOT_A_HARNESS";
  internal const string API_DUPLICATE = "LOOM_API_DUPLICATE";
  internal const string VERB_ATTRIBUTE_MISSING = "LOOM_VERB_ATTRIBUTE_MISSING";
  internal const string VERB_NAME_INVALID = "LOOM_VERB_NAME_INVALID";
  internal const string VERB_NO_SCOPE = "LOOM_VERB_NO_SCOPE";
  internal const string VERB_DUPLICATE = "LOOM_VERB_DUPLICATE";
  internal const string VERB_OUT_OF_SCOPE = "LOOM_VERB_OUT_OF_SCOPE";
  internal const string THREAD_NOT_TOP_LEVEL = "LOOM_THREAD_NOT_TOP_LEVEL";
  internal const string THREAD_NAME_NOT_LITERAL = "LOOM_THREAD_NAME_NOT_LITERAL";
  internal const string THREAD_NOT_PREPARED = "LOOM_THREAD_NOT_PREPARED";
  internal const string DRAFT_STATEMENT_FAILED = "LOOM_DRAFT_STATEMENT_FAILED";
  internal const string HARNESS_NOT_FIRST_PARTY = "LOOM_HARNESS_NOT_FIRST_PARTY";
  internal const string HARNESS_INSTALL_FAILED = "LOOM_HARNESS_INSTALL_FAILED";
  internal const string HARNESS_UPDATE_FAILED = "LOOM_HARNESS_UPDATE_FAILED";
  internal const string HARNESS_VERSION_CONFLICT = "LOOM_HARNESS_VERSION_CONFLICT";
  internal const string THREAD_VERSION_NOT_LITERAL = "LOOM_THREAD_VERSION_NOT_LITERAL";
  internal const string THREAD_VERSION_CONFLICT = "LOOM_THREAD_VERSION_CONFLICT";
  internal const string REWEAVE_REQUIRES_RESTART = "LOOM_REWEAVE_REQUIRES_RESTART";
  internal const string REWEAVE_REVERT_FAILED = "LOOM_REWEAVE_REVERT_FAILED";

  private LoomException(string errorId, ErrorCategory errorCategory, string message, object? targetObject, Exception? innerException = null)
    : base(errorId, errorCategory, message, targetObject, innerException) { }

  internal static LoomException HarnessNameInvalid(string name, Type harnessType)
    => new(HARNESS_NAME_INVALID, ErrorCategory.InvalidData,
      $"'{harnessType.FullName}' declares the harness name '{name}'. A harness name is a letter followed by letters or digits.", harnessType);

  internal static LoomException HarnessDuplicate(string name, Type existing, Type attempted)
    => new(HARNESS_DUPLICATE, ErrorCategory.ResourceExists,
      $"The harness name '{name}' is already used by '{existing.FullName}'; '{attempted.FullName}' cannot use it too.", attempted);

  internal static LoomException ComposeFailed(string name, Exception innerException)
    => new(HARNESS_COMPOSE_FAILED, ErrorCategory.InvalidOperation, $"The harness '{name}' failed to compose: {innerException.Message}", name,
      innerException);

  internal static LoomException HarnessNotComposed(Type harnessType)
    => new(HARNESS_NOT_COMPOSED, ErrorCategory.ObjectNotFound,
      $"The harness '{harnessType.FullName}' is not composed in this runspace. Import its module first.", harnessType);

  internal static LoomException HarnessNotInstalled(string name, string moduleName, Version? version, Exception? innerException)
    => new(HARNESS_NOT_INSTALLED, ErrorCategory.ObjectNotFound,
      $"The harness '{name}' needs the module '{moduleName}'{(version is null ? string.Empty : $" {version}")}, which is not installed. " +
      $"Install it with: {InstallCommand(moduleName, version)}", moduleName, innerException);

  internal static LoomException HarnessImportFailed(string name, string moduleName, Exception innerException)
    => new(HARNESS_IMPORT_FAILED, ErrorCategory.InvalidOperation,
      $"Importing '{moduleName}' for the harness '{name}' failed: {Unwrap(innerException).Message}", moduleName, innerException);

  internal static LoomException NotAHarness(string name, string moduleName)
    => new(NOT_A_HARNESS, ErrorCategory.InvalidData, $"The module '{moduleName}' was imported but did not register a harness named '{name}'.",
      moduleName);

  internal static LoomException ApiDuplicate(Type apiType, string existingOwner, string attemptedOwner)
    => new(API_DUPLICATE, ErrorCategory.ResourceExists,
      $"'{apiType.FullName}' is already published by the harness '{existingOwner}'; '{attemptedOwner}' cannot publish it too.", apiType);

  internal static LoomException VerbAttributeMissing(Type verbType)
    => new(VERB_ATTRIBUTE_MISSING, ErrorCategory.InvalidData, $"'{verbType.FullName}' is not marked with [LoomVerb(\"<name>\", <scopes>)].",
      verbType);

  internal static LoomException VerbNameInvalid(string name, Type verbType)
    => new(VERB_NAME_INVALID, ErrorCategory.InvalidData,
      $"'{verbType.FullName}' declares the verb name '{name}'. A verb name is a letter followed by letters or digits.", verbType);

  internal static LoomException VerbNoScope(Type verbType)
    => new(VERB_NO_SCOPE, ErrorCategory.InvalidData, $"'{verbType.FullName}' does not list any scope in its [LoomVerb] attribute.", verbType);

  internal static LoomException VerbDuplicate(string name, Type scope, string existingOwner, string attemptedOwner)
    => new(VERB_DUPLICATE, ErrorCategory.ResourceExists,
      $"The verb '{name}' in scope '{scope.Name}' is already registered by '{existingOwner}'; '{attemptedOwner}' cannot register it too.", name);

  internal static LoomException VerbOutOfScope(string name, Type currentScope, IEnumerable<Type> validScopes, IScriptExtent? extent)
    => new(VERB_OUT_OF_SCOPE, ErrorCategory.InvalidOperation,
      $"'{name}' cannot be used in scope '{currentScope.Name}'; it is valid in: {string.Join(", ", validScopes.Select(scope => scope.Name))}." +
      Where(extent), name);

  internal static LoomException VerbOutOfScope(string name, Type currentScope, IEnumerable<Type> validScopes, int? line)
    => new(VERB_OUT_OF_SCOPE, ErrorCategory.InvalidOperation,
      $"'{name}' cannot be used in scope '{currentScope.Name}'; it is valid in: {string.Join(", ", validScopes.Select(scope => scope.Name))}." +
      (line is > 0 ? $" (line {line})" : string.Empty), name);

  internal static LoomException ThreadNotTopLevel(IScriptExtent extent)
    => new(THREAD_NOT_TOP_LEVEL, ErrorCategory.InvalidOperation,
      "Thread must be a top-level statement of the draft, not inside a condition, loop or block, so harness loading is always visible." +
      Where(extent), extent.Text);

  internal static LoomException ThreadNameNotLiteral(IScriptExtent extent)
    => new(THREAD_NAME_NOT_LITERAL, ErrorCategory.InvalidArgument,
      "Thread needs a literal harness name (for example: Thread Reed) so harnesses can load before the draft runs." + Where(extent), extent.Text);

  internal static LoomException ThreadNotPrepared(string name)
    => new(THREAD_NOT_PREPARED, ErrorCategory.InvalidOperation,
      $"The harness '{name}' was not loaded before the draft ran. Write Thread with a literal name as a top-level statement of the draft.", name);

  internal static LoomException HarnessNotFirstParty(string name, IEnumerable<string> firstParty)
    => new(HARNESS_NOT_FIRST_PARTY, ErrorCategory.PermissionDenied,
      $"Thread only loads first-party harnesses ({string.Join(", ", firstParty.Order(StringComparer.OrdinalIgnoreCase))}); '{name}' is not one of them.",
      name);

  internal static LoomException HarnessInstallFailed(string name, string moduleName, Version? version, string reason, Exception? innerException)
    => new(HARNESS_INSTALL_FAILED, ErrorCategory.ResourceUnavailable,
      $"The harness '{name}' could not be installed: {reason} Install it manually with: {InstallCommand(moduleName, version)}", moduleName,
      innerException);

  internal static LoomException HarnessUpdateFailed(string moduleName, Exception innerException)
    => new(HARNESS_UPDATE_FAILED, ErrorCategory.ResourceUnavailable, $"Updating '{moduleName}' failed: {Unwrap(innerException).Message}",
      moduleName, innerException);

  internal static LoomException HarnessVersionConflict(string name, Version requested, Version loaded)
    => new(HARNESS_VERSION_CONFLICT, ErrorCategory.InvalidOperation,
      $"The draft asks for harness '{name}' {requested}, but {loaded} is already loaded in this session. Restart the session to switch versions.",
      name);

  internal static LoomException ThreadVersionNotLiteral(IScriptExtent extent)
    => new(THREAD_VERSION_NOT_LITERAL, ErrorCategory.InvalidArgument,
      "Thread -Version needs a literal version such as 1.2.0." + Where(extent), extent.Text);

  internal static LoomException ThreadVersionConflict(string name, IScriptExtent extent)
    => new(THREAD_VERSION_CONFLICT, ErrorCategory.InvalidArgument,
      $"The draft threads '{name}' more than once with different versions." + Where(extent), name);

  internal static string ReweaveRequiresRestart(IEnumerable<string> verbKeys)
    => $"{REWEAVE_REQUIRES_RESTART}: these statements were removed from the draft but cannot be undone in a running session; restart the " +
       $"session to drop them: {string.Join(", ", verbKeys)}.";

  internal static LoomException ReweaveRevertFailed(string verbName, Exception innerException)
    => new(REWEAVE_REVERT_FAILED, ErrorCategory.InvalidOperation,
      $"Undoing a removed '{verbName}' statement failed: {Unwrap(innerException).Message}", verbName, innerException);

  internal static string InstallCommand(string moduleName, Version? version)
    => $"Install-PSResource -Name {moduleName}{(version is null ? string.Empty : $" -Version {version}")} -Repository PSGallery -Scope CurrentUser";

  internal static ErrorRecord FromStatement(Exception exception)
    => exception switch {
      PowerShellException powerShellException => powerShellException.ToErrorRecord(),
      IContainsErrorRecord { ErrorRecord: { } record } => record,
      var _ => new ErrorRecord(exception, DRAFT_STATEMENT_FAILED, ErrorCategory.NotSpecified, null)
    };

  private static string Where(IScriptExtent? extent)
    => extent is null ? string.Empty : $" (line {extent.StartLineNumber}, column {extent.StartColumnNumber})";

  private static Exception Unwrap(Exception exception)
    => exception is RuntimeException { InnerException: { } inner } and (CmdletInvocationException or MethodInvocationException) ? inner : exception;
}
