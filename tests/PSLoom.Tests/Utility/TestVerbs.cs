// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hosting;

namespace PSLoom.Tests.Utility;

// Verbs and harnesses compiled into the test assembly, for registry, validator and registration tests.

public abstract class CrateScope : DslScope;

public sealed class CrateFrame(string name) : IDslFrame<CrateScope> {
  public string Name { get; } = name;

  public List<string> Contents { get; } = [];
}

[LoomVerb("Crate", typeof(DraftScope))]
public sealed class CrateVerb : LoomVerb {
  [Parameter(Mandatory = true, Position = 0)]
  public string Name { get; set; } = null!;

  [Parameter]
  public SwitchParameter Sealed { get; set; }

  [Parameter(Position = 1)]
  [OpensScope(typeof(CrateScope))]
  public ScriptBlock? Body { get; set; }

  public static List<CrateFrame> Built { get; } = [];

  protected override void Weave() {
    var frame = new CrateFrame(Name);
    Loom.RunScoped(frame, Body);

    lock (Built) {
      Built.Add(frame);
    }
  }
}

[LoomVerb("Bottle", typeof(CrateScope))]
public sealed class BottleVerb : LoomVerb {
  [Parameter(Mandatory = true, Position = 0)]
  [Alias("Label")]
  public string Name { get; set; } = null!;

  protected override void Weave()
    => Loom.RequireFrame<CrateFrame>().Contents.Add(Name);
}

[LoomVerb("Crate", typeof(DraftScope))]
public sealed class RivalCrateVerb : LoomVerb {
  protected override void Weave() { }
}

public sealed class UnmarkedVerb : LoomVerb {
  protected override void Weave() { }
}

[LoomVerb("9lives", typeof(DraftScope))]
public sealed class BadNameVerb : LoomVerb {
  protected override void Weave() { }
}

[LoomVerb("Lonely")]
public sealed class NoScopeVerb : LoomVerb {
  protected override void Weave() { }
}

[Harness("Crates", Description = "Crates for tests")]
public sealed class CratesHarness : IHarness {
  public void Compose(IHarnessBuilder builder) {
    builder.Verbs.Add<CrateVerb>();
    builder.Verbs.Add<BottleVerb>();
    builder.Harnesses.Publish<ICrateApi>(new CrateApi());
  }
}

[Harness("Rival")]
public sealed class RivalHarness : IHarness {
  public void Compose(IHarnessBuilder builder)
    => builder.Verbs.Add<RivalCrateVerb>();
}

[Harness("Broken")]
public sealed class BrokenHarness : IHarness {
  public void Compose(IHarnessBuilder builder) {
    builder.Verbs.Add<UnmarkedVerbHolder.Marked>();
    throw new InvalidOperationException("compose exploded");
  }
}

[Harness("Crates")]
public sealed class CratesImpostorHarness : IHarness {
  public void Compose(IHarnessBuilder builder) { }
}

[Harness("not-valid")]
public sealed class InvalidNameHarness : IHarness {
  public void Compose(IHarnessBuilder builder) { }
}

[Harness("Publisher")]
public sealed class ApiThiefHarness : IHarness {
  public void Compose(IHarnessBuilder builder)
    => builder.Harnesses.Publish<ICrateApi>(new CrateApi());
}

public interface ICrateApi {
  string Kind { get; }
}

public sealed class CrateApi : ICrateApi {
  public string Kind => "wooden";
}

public static class UnmarkedVerbHolder {
  [LoomVerb("Marked", typeof(DraftScope))]
  public sealed class Marked : LoomVerb {
    protected override void Weave() { }
  }
}
