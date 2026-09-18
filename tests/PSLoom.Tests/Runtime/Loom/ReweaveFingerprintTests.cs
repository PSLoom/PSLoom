// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections;
using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Runtime.Loom;

namespace PSLoom.Tests.Runtime.Loom;

[TestSubject(typeof(ReweaveFingerprint))]
public sealed class ReweaveFingerprintTests {
  [Fact]
  public void Fingerprint_IgnoresParameterOrderAndNameCase() {
    var first = ReweaveFingerprint.Compute(new Dictionary<string, object?> { ["Context"] = "a:*", ["Name"] = "b", ["Value"] = 1 });
    var second = ReweaveFingerprint.Compute(new Dictionary<string, object?> { ["value"] = 1, ["NAME"] = "b", ["context"] = "a:*" });

    first.ShouldBe(second);
  }

  [Theory]
  [MemberData(nameof(DistinctValues))]
  public void Fingerprint_DistinguishesValues(object? left, object? right)
    => Compute(left).ShouldNotBe(Compute(right));

  [Fact]
  public void Fingerprint_UnwrapsPSObjectsAndUsesScriptBlockText() {
    Compute(PSObject.AsPSObject("x")).ShouldBe(Compute("x"));
    Compute(ScriptBlock.Create("Item a")).ShouldBe(Compute(ScriptBlock.Create("Item a")));
    Compute(ScriptBlock.Create("Item a")).ShouldNotBe(Compute(ScriptBlock.Create("Item b")));
  }

  [Fact]
  public void Fingerprint_HashtablesIgnoreKeyOrder()
    => Compute(new Hashtable { ["a"] = 1, ["b"] = 2 }).ShouldBe(Compute(new Hashtable { ["b"] = 2, ["a"] = 1 }));

  [Fact]
  public void Key_UsesReweaveKeyValues_OrFingerprintWithoutKeys() {
    var bound = new Dictionary<string, object?> { ["Context"] = "a:*", ["Name"] = "b", ["Value"] = 1 };
    var fingerprint = ReweaveFingerprint.Compute(bound);

    var keyed = ReweaveFingerprint.Key("Style", ["Context", "Name"], bound, fingerprint);
    var keyless = ReweaveFingerprint.Key("Fail", [], bound, fingerprint);

    keyed.ShouldContain("a:*");
    keyless.ShouldContain(fingerprint.ToString("x32"));
    ReweaveFingerprint.Key("Style", ["Context", "Name"], new Dictionary<string, object?>(bound) { ["Value"] = 2 }, 0).ShouldBe(keyed);
  }

  [Fact]
  public void DisambiguateKey_NumbersRepeatedKeys() {
    var run = new LoomRun(true);

    run.DisambiguateKey("Style a").ShouldBe("Style a");
    run.DisambiguateKey("Style a").ShouldBe("Style a#2");
    run.DisambiguateKey("Style b").ShouldBe("Style b");
  }

  public static TheoryData<object?, object?> DistinctValues()
    => new() {
      { "1", 1 },
      { 1, 1L },
      { "ab", new[] { "a", "b" } },
      { new[] { "a", "b" }, new[] { "b", "a" } },
      { null, "" },
      { true, new SwitchParameter(false) },
      { new Hashtable { ["a"] = 1 }, new Hashtable { ["a"] = 2 } }
    };

  private static UInt128 Compute(object? value)
    => ReweaveFingerprint.Compute(new Dictionary<string, object?> { ["Value"] = value });
}
