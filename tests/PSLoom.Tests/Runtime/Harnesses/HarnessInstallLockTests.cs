// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Runtime.Harnesses;

namespace PSLoom.Tests.Runtime.Harnesses;

[TestSubject(typeof(HarnessInstallLock))]
[TestSubject(typeof(HarnessProvisioner))]
public sealed class HarnessInstallLockTests : IDisposable {
  private readonly string _directory = Directory.CreateTempSubdirectory("psloom-lock-").FullName;

  public void Dispose()
    => Directory.Delete(_directory, true);

  [Fact]
  public void Acquire_CreatesDirectoriesAndReleasesOnDispose() {
    var path = Path.Combine(_directory, "nested", "install.lock");
    var installLock = new HarnessInstallLock(path, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(10));

    installLock.Acquire().Dispose();

    File.Exists(path).ShouldBeTrue();
    Should.NotThrow(() => installLock.Acquire().Dispose());
  }

  [Fact]
  public async Task Acquire_WhileHeld_WaitsReportsOnceAndSucceedsAfterRelease() {
    var installLock = new HarnessInstallLock(Path.Combine(_directory, "install.lock"), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10));
    var messages = new List<string>();
    var held = installLock.Acquire();

    var waiter = Task.Run(() => installLock.Acquire(messages.Add), TestContext.Current.CancellationToken);
    await Task.Delay(150, TestContext.Current.CancellationToken);
    waiter.IsCompleted.ShouldBeFalse();

    held.Dispose();
    using var acquired = await waiter.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    messages.ShouldBe([HarnessInstallLock.WAITING_MESSAGE]);
  }

  [Fact]
  public void Acquire_HeldPastTimeout_Throws() {
    var installLock = new HarnessInstallLock(Path.Combine(_directory, "install.lock"), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(10));
    using var held = installLock.Acquire();

    Should.Throw<TimeoutException>(() => installLock.Acquire());
  }

  [Fact]
  public void Reason_UnwrapsPowerShellStopAndInvocationWrappers() {
    using var shell = PowerShell.Create();
    var stop = Should.Throw<ActionPreferenceStopException>(() =>
      shell.AddScript("Write-Error \"Package(s) 'PSLoom.Colorway' could not be installed\" -ErrorAction Stop").Invoke());

    stop.Message.ShouldContain("preference variable");
    HarnessProvisioner.Reason(new MethodInvocationException("wrapper", stop)).ShouldBe("Package(s) 'PSLoom.Colorway' could not be installed.");
    HarnessProvisioner.Reason(new InvalidOperationException("plain.")).ShouldBe("plain.");
  }

  [Theory]
  [InlineData("1.2", "1.2.0.0", true)]
  [InlineData("1.2.0", "1.2.0.0", true)]
  [InlineData("1.2.3", "1.2.0.0", false)]
  [InlineData("2.0", "1.2.0.0", false)]
  public void VersionsMatch_TreatsMissingComponentsAsZero(string requested, string actual, bool expected)
    => HarnessProvisioner.VersionsMatch(Version.Parse(requested), Version.Parse(actual)).ShouldBe(expected);
}
