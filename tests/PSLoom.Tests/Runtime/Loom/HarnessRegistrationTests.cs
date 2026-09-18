// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Runtime.Harnesses;
using PSLoom.Runtime.Loom;
using PSLoom.TestKit;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Hosting;

namespace PSLoom.Tests.Runtime.Loom;

[TestSubject(typeof(HarnessRegistry))]
[TestSubject(typeof(Kernel))]
public sealed class HarnessRegistrationTests {
  [Fact]
  public void Register_ComposesHarnessWithIdentityAndServices() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);

    HarnessHost.Register<CratesHarness>();

    session.Loom.Harnesses.TryGetByName("crates", out var services).ShouldBeTrue();
    services.Identity.Name.ShouldBe("Crates");
    services.Identity.Description.ShouldBe("Crates for tests");
    services.Identity.ModuleName.ShouldBe(typeof(CratesHarness).Assembly.GetName().Name);
    services.Identity.CompiledWarpVersion.Major.ShouldBe(WarpContract.Version.Major);
    services.Storage.Root.FullPath.ShouldEndWith("Crates");
    services.Verbs.ShouldNotBeNull();
    session.Loom.Verbs.FindByName("Bottle").ShouldHaveSingleItem().Owner.ShouldBe("Crates");
    HarnessHost.Current<CratesHarness>().ShouldBeSameAs(services);
  }

  [Fact]
  public void Register_SameHarnessTwice_ComposesOnce() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);

    HarnessHost.Register<CratesHarness>();
    Should.NotThrow(HarnessHost.Register<CratesHarness>);

    session.Loom.Harnesses.All.ShouldHaveSingleItem();
  }

  [Fact]
  public void Register_IsPerRunspace() {
    using var first = new KernelSession();
    using var second = new KernelSession();

    using (PowerShellHost.UseAsDefault(first.Runspace)) {
      HarnessHost.Register<CratesHarness>();
    }

    second.Loom.Harnesses.All.ShouldBeEmpty();

    using (PowerShellHost.UseAsDefault(second.Runspace)) {
      Should.Throw<LoomException>(() => HarnessHost.Current<CratesHarness>()).ErrorId.ShouldBe(LoomException.HARNESS_NOT_COMPOSED);
    }
  }

  [Fact]
  public void Register_ComposeFailure_RollsBackVerbsAndSurfacesError() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);

    var exception = Should.Throw<LoomException>(HarnessHost.Register<BrokenHarness>);

    exception.ErrorId.ShouldBe(LoomException.HARNESS_COMPOSE_FAILED);
    exception.InnerException!.Message.ShouldBe("compose exploded");
    session.Loom.Verbs.FindByName("Marked").ShouldBeEmpty();
    session.Loom.Harnesses.TryGetByName("Broken", out var _).ShouldBeFalse();
  }

  [Fact]
  public void Register_DuplicateVerbFromAnotherHarness_FailsComposeNamingBothOwners() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);
    HarnessHost.Register<CratesHarness>();

    var exception = Should.Throw<LoomException>(HarnessHost.Register<RivalHarness>);

    exception.ErrorId.ShouldBe(LoomException.HARNESS_COMPOSE_FAILED);
    exception.Message.ShouldContain("'Crates'");
    exception.Message.ShouldContain("'Rival'");
  }

  [Fact]
  public void Register_NameTakenByAnotherType_IsRejected() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);
    HarnessHost.Register<CratesHarness>();

    Should.Throw<LoomException>(HarnessHost.Register<CratesImpostorHarness>).ErrorId.ShouldBe(LoomException.HARNESS_DUPLICATE);
  }

  [Fact]
  public void Register_InvalidName_IsRejected() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);

    Should.Throw<LoomException>(HarnessHost.Register<InvalidNameHarness>).ErrorId.ShouldBe(LoomException.HARNESS_NAME_INVALID);
  }

  [Fact]
  public void Directory_PublishedApi_IsVisibleToOthers_AndCannotBeTakenOver() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);
    HarnessHost.Register<CratesHarness>();

    HarnessHost.Current<CratesHarness>().Harnesses.TryGet<ICrateApi>(out var api).ShouldBeTrue();
    api.Kind.ShouldBe("wooden");

    var exception = Should.Throw<LoomException>(HarnessHost.Register<ApiThiefHarness>);
    exception.InnerException.ShouldBeOfType<LoomException>().ErrorId.ShouldBe(LoomException.API_DUPLICATE);
  }

  [Fact]
  public void DslRunner_RunsBodyOutsideADraft_WithScopedVerbsAndCollectedErrors() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);
    HarnessHost.Register<CratesHarness>();
    var frame = new CrateFrame("standalone");

    var errors = HarnessHost.Current<CratesHarness>().Dsl.Run(frame, ScriptBlock.Create("Bottle one; Bottle -Label two; Crate nope"));

    frame.Contents.ShouldBe(["one", "two"]);
    // Only CrateScope verbs are defined in the body, so a draft verb is simply an unknown command there.
    errors.ShouldHaveSingleItem().CategoryInfo.Category.ShouldBe(ErrorCategory.ObjectNotFound);
    session.Loom.CurrentRun.ShouldBeNull();
  }

  [Fact]
  public void DslRunner_TerminatingStatement_IsReportedAndStopsTheBody() {
    using var session = new KernelSession();
    using var defaultRunspace = PowerShellHost.UseAsDefault(session.Runspace);
    HarnessHost.Register<CratesHarness>();
    var frame = new CrateFrame("standalone");

    var errors = HarnessHost.Current<CratesHarness>().Dsl.Run(frame, ScriptBlock.Create("Bottle one; throw 'stop'; Bottle two"));

    frame.Contents.ShouldBe(["one"]);
    errors.ShouldHaveSingleItem().Exception.Message.ShouldBe("stop");
  }
}
