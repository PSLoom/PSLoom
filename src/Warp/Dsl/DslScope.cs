// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   A DSL scope, identified by its type. Declare one per nesting level a harness introduces, as an <see langword="abstract" />
///   class: <c>public abstract class CompleterScope : DslScope;</c>. Types cannot collide the way scope-name strings would, and
///   they make nesting statically checkable. Scope types are tokens used only through <see langword="typeof" /> and generic
///   arguments; state belongs in the scope's <see cref="IDslFrame{TScope}" />.
/// </summary>
/// <remarks>
///   <see cref="IVerbRegistry.Add{TVerb}" /> rejects any scope that could be instantiated: it must be
///   <see langword="abstract" />, or <see langword="sealed" /> with only private constructors (like <see cref="DraftScope" />).
/// </remarks>
public abstract class DslScope;
