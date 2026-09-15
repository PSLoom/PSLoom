// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Verbs;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   One top-level verb invocation recorded by a draft run, with the verb that produced it.
/// </summary>
internal sealed record LedgerItem(ReweaveEntry Entry, VerbDescriptor Verb);
