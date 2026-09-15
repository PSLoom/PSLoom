// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   A top-level <c>Thread</c> statement found by the prepass.
/// </summary>
/// <param name="Name">The harness name.</param>
/// <param name="Version">The pinned version, when <c>-Version</c> was given.</param>
/// <param name="Extent">Where the statement is.</param>
internal sealed record ThreadDeclaration(string Name, Version? Version, IScriptExtent Extent);
