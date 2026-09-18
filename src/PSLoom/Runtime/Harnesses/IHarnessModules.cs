// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   The PowerShell module operations harness provisioning needs. The seam lets tests drive the policy without a gallery.
/// </summary>
internal interface IHarnessModules {
  /// <summary>
  ///   Imports a module into the global scope, pinned to a version when given.
  /// </summary>
  ModuleImportResult Import(string moduleName, Version? version);

  /// <summary>
  ///   Installs a module for the current user from PSGallery. Throws when the install fails.
  /// </summary>
  void Install(string moduleName, Version? version);

  /// <summary>
  ///   Shows a progress message to the user, visible without <c>-Verbose</c>.
  /// </summary>
  void WriteProgress(string message);
}
