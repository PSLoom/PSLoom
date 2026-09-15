// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   A cross-process lock serializing harness installs and updates, so shells starting together do not install the same module
///   concurrently. Backed by an exclusively opened file under the Creel.
/// </summary>
internal sealed class HarnessInstallLock(string filePath, TimeSpan timeout, TimeSpan pollInterval) {
  internal const string WAITING_MESSAGE = "Waiting for another PowerShell session to finish installing harnesses…";

  /// <summary>
  ///   Gets the lock at <c>&lt;creel&gt;/kernel/locks/install.lock</c>, waiting up to five minutes.
  /// </summary>
  public static HarnessInstallLock ForCreel()
    => new(Path.Combine(CreelRoot.Resolve(), "kernel", "locks", "install.lock"), TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(250));

  public string FilePath { get; } = filePath;

  /// <summary>
  ///   Acquires the lock, reporting once when it has to wait.
  /// </summary>
  /// <returns>A handle that releases the lock when disposed.</returns>
  /// <exception cref="TimeoutException">The lock was not acquired within the timeout.</exception>
  public IDisposable Acquire(Action<string>? onWait = null) {
    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

    var started = Stopwatch.GetTimestamp();
    var reported = false;

    while (true) {
      try {
        return new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1);
      }
      catch (IOException) when (Stopwatch.GetElapsedTime(started) < timeout) {
        if (!reported) {
          onWait?.Invoke(WAITING_MESSAGE);
          reported = true;
        }

        Thread.Sleep(pollInterval);
      }
      catch (IOException exception) {
        throw new TimeoutException($"Another PowerShell session held the harness install lock ({FilePath}) for more than {timeout}.", exception);
      }
    }
  }
}
