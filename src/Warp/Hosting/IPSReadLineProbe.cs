// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Hosting;

/// <summary>
///   Answers whether the loaded PSReadLine supports a capability, so callers can warn and no-op instead of letting a
///   <c>ParameterBindingException</c> reach the user.
/// </summary>
public interface IPSReadLineProbe {
  /// <summary>
  ///   Gets a value indicating whether PSReadLine is loaded in this runspace.
  /// </summary>
  bool IsLoaded { get; }

  /// <summary>
  ///   Checks whether <c>Set-PSReadLineOption</c> exposes a parameter (e.g. <c>TokenColorHandler</c> from the fork).
  /// </summary>
  /// <param name="parameterName">The parameter name, without a leading dash.</param>
  /// <returns><see langword="true" /> when PSReadLine is loaded and the parameter exists.</returns>
  bool HasOptionParameter(string parameterName);
}
