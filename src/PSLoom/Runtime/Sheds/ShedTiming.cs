// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   When a staged statement applies.
/// </summary>
public enum ShedTiming {
  /// <summary>During the draft, like an unstaged statement; only conditions, output and <c>-AtLoad</c> change.</summary>
  Now,

  /// <summary>After the first prompt, in slot <c>0a</c>.</summary>
  Wait,

  /// <summary>After the first prompt, in the declared slot.</summary>
  Slot
}
