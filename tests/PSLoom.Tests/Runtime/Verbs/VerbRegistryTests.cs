// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Verbs;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Dsl;

namespace PSLoom.Tests.Runtime.Verbs;

[TestSubject(typeof(VerbRegistry))]
public sealed class VerbRegistryTests {
  [Fact]
  public void Add_IndexesVerbByEveryScopeItListsAndBuildsTables() {
    var registry = new VerbRegistry();

    registry.Add(typeof(CrateVerb), "Crates");
    registry.Add(typeof(BottleVerb), "Crates");

    registry.TableFor(typeof(DraftScope)).Keys.ShouldBe(["Crate"]);
    registry.TableFor(typeof(CrateScope)).Keys.ShouldBe(["Bottle"]);
    registry.FindByName("crate").ShouldHaveSingleItem().Owner.ShouldBe("Crates");
  }

  [Fact]
  public void Add_SameTypeTwice_IsIdempotent() {
    var registry = new VerbRegistry();

    var first = registry.Add(typeof(CrateVerb), "Crates");
    var generation = registry.Generation;
    var second = registry.Add(typeof(CrateVerb), "Crates");

    second.ShouldBeSameAs(first);
    registry.Generation.ShouldBe(generation);
  }

  [Fact]
  public void Add_DuplicateNameInScope_NamesBothOwners() {
    var registry = new VerbRegistry();
    registry.Add(typeof(CrateVerb), "Crates");

    var exception = Should.Throw<LoomException>(() => registry.Add(typeof(RivalCrateVerb), "Rival"));

    exception.ErrorId.ShouldBe(LoomException.VERB_DUPLICATE);
    exception.Message.ShouldContain("'Crates'");
    exception.Message.ShouldContain("'Rival'");
  }

  [Theory]
  [InlineData(typeof(UnmarkedVerb), LoomException.VERB_ATTRIBUTE_MISSING)]
  [InlineData(typeof(BadNameVerb), LoomException.VERB_NAME_INVALID)]
  [InlineData(typeof(NoScopeVerb), LoomException.VERB_NO_SCOPE)]
  public void Add_InvalidVerb_IsRejected(Type verbType, string errorId)
    => Should.Throw<LoomException>(() => new VerbRegistry().Add(verbType, "Tests")).ErrorId.ShouldBe(errorId);

  [Fact]
  public void RemoveOwner_RemovesItsVerbsAndInvalidatesTables() {
    var registry = new VerbRegistry();
    registry.Add(typeof(CrateVerb), "Crates");
    var before = registry.TableFor(typeof(DraftScope));

    registry.RemoveOwner("Crates");

    registry.TableFor(typeof(DraftScope)).ShouldNotBeSameAs(before);
    registry.TableFor(typeof(DraftScope)).ShouldBeEmpty();
    registry.ForType(typeof(CrateVerb)).ShouldBeNull();
  }

  [Fact]
  public void TableFor_UnchangedRegistry_ReturnsCachedTable() {
    var registry = new VerbRegistry();
    registry.Add(typeof(CrateVerb), "Crates");

    registry.TableFor(typeof(DraftScope)).ShouldBeSameAs(registry.TableFor(typeof(DraftScope)));
  }

  [Fact]
  public void MatchParameter_UsesExactNameAliasThenUniquePrefix() {
    var crate = new VerbRegistry().Add(typeof(CrateVerb), "Crates");
    var bottle = new VerbRegistry().Add(typeof(BottleVerb), "Crates");

    crate.MatchParameter("Body")!.Name.ShouldBe("Body");
    crate.MatchParameter("se")!.Name.ShouldBe("Sealed");
    crate.MatchParameter("x").ShouldBeNull();
    bottle.MatchParameter("label")!.Name.ShouldBe("Name");
    crate.MatchParameter("Sealed")!.IsSwitch.ShouldBeTrue();
    crate.MatchParameter("Body")!.OpensScope.ShouldBe(typeof(CrateScope));
  }

  [Fact]
  public void Shim_InvokesTheVerbCommand() {
    var descriptor = new VerbRegistry().Add(typeof(CrateVerb), "Crates");

    VerbShims.Get(descriptor.ShimId).ImplementingType.ShouldBe(typeof(CrateVerb));
    descriptor.Shim.ToString().ShouldContain($"Get({descriptor.ShimId})");
  }
}
