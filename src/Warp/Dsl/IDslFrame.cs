// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   State a harness attaches to one active scope (e.g. the completer being built). The frame names its scope, so
///   <see cref="ILoomContext.RunScoped{TScope}" /> infers the scope and a frame can never be pushed into the wrong one.
/// </summary>
/// <typeparam name="TScope">The scope this frame belongs to.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Type-level scope binding.")]
public interface IDslFrame<TScope> where TScope : DslScope;
