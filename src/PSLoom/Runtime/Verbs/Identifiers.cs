// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   The naming rule shared by harness and verb names: a letter followed by letters or digits.
/// </summary>
internal static class Identifiers {
  public static bool IsValid(string? name) {
    if (string.IsNullOrEmpty(name) ||
        !char.IsAsciiLetter(name[0])) {
      return false;
    }

    foreach (var character in name) {
      if (!char.IsAsciiLetterOrDigit(character)) {
        return false;
      }
    }

    return true;
  }
}
