// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

// RoslynCodeTaskFactory fragment. The lock covers version cleanup and the entire copy, across MSBuild processes.
var source = Path.GetFullPath(Source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
var root = Path.GetFullPath(Destination);
var match = System.Text.RegularExpressions.Regex.Match(PackageVersion, @"^(\d+\.\d+\.\d+)(?:[-+].*)?$");
if (!match.Success) {
  Log.LogError("Invalid PSLoomVersion: {0}", PackageVersion);
  return false;
}
var parent = Path.GetDirectoryName(root);
Directory.CreateDirectory(parent);
var lockPath = Path.Combine(parent, ".PSLoom.restore.lock");
FileStream lease = null;
var deadline = DateTime.UtcNow.AddMinutes(2);
while (lease == null) {
  try {
    lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
  }
  catch (IOException) {
    if (DateTime.UtcNow >= deadline) throw;
    Thread.Sleep(100);
  }
}
using (lease) {
  var destination = Path.Combine(root, match.Groups[1].Value);
  var marker = Path.Combine(root, ".package-version");
  var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
  if (!files.Any(file => Path.GetFileName(file) == "Warp.dll") ||
      !files.Any(file => Path.GetFileName(file) == "PSLoom.dll")) {
    Log.LogError("The PSLoom package module must contain PSLoom.dll and Warp.dll.");
    return false;
  }
  // A completed identical restore is read-only, so a parallel test host may safely keep these assemblies loaded.
  if (File.Exists(marker) && File.ReadAllText(marker) == PackageVersion &&
      Directory.Exists(destination) && Directory.GetDirectories(root).Length == 1 &&
      files.All(file => File.Exists(Path.Combine(destination, file.Substring(source.Length + 1))))) return true;

  Directory.CreateDirectory(root);
  File.Delete(marker);
  foreach (var directory in Directory.GetDirectories(root)) Directory.Delete(directory, true);
  foreach (var file in files) {
    var target = Path.Combine(destination, file.Substring(source.Length + 1));
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    File.Copy(file, target, true);
  }
  File.WriteAllText(marker, PackageVersion);
  Log.LogMessage(Microsoft.Build.Framework.MessageImportance.High, "PSLoom {0} -> {1}", PackageVersion, destination);
}
