// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Warp.Dsl;

namespace PSLoom.Warp.Tests.Dsl;

[TestSubject(typeof(DslScopeRules))]
public sealed class DslScopeRulesTests {
  [Theory]
  [InlineData(typeof(AbstractScope))]
  [InlineData(typeof(DraftScope))]
  [InlineData(typeof(SealedPrivateScope))]
  public void TokenOnlyScopesAreAccepted(Type scope) {
    Should.NotThrow(() => DslScopeRules.EnsureValid(scope, typeof(DslScopeRulesTests)));
  }

  [Theory]
  [InlineData(typeof(SealedPublicScope))]
  [InlineData(typeof(OpenScope))]
  [InlineData(typeof(SealedInternalScope))]
  public void InstantiableScopesAreRejected(Type scope) {
    var exception = Should.Throw<WarpException>(() => DslScopeRules.EnsureValid(scope, typeof(DslScopeRulesTests)));

    exception.ErrorId.ShouldBe(WarpException.SCOPE_INVALID);
    exception.Message.ShouldContain($"public abstract class {scope.Name} : DslScope;");
    exception.TargetObject.ShouldBe(scope);
  }

  [Fact]
  public void TypesNotDerivingFromDslScopeAreRejected() {
    Should.Throw<WarpException>(() => DslScopeRules.EnsureValid(typeof(string), typeof(DslScopeRulesTests)))
      .ErrorId.ShouldBe(WarpException.SCOPE_INVALID);
  }

  [Fact]
  public void DslScopeItselfIsRejected() {
    Should.Throw<WarpException>(() => DslScopeRules.EnsureValid(typeof(DslScope), typeof(DslScopeRulesTests)))
      .ErrorId.ShouldBe(WarpException.SCOPE_INVALID);
  }

  [Fact]
  public void OpenGenericScopesAreRejected() {
    Should.Throw<WarpException>(() => DslScopeRules.EnsureValid(typeof(GenericScope<>), typeof(DslScopeRulesTests)))
      .ErrorId.ShouldBe(WarpException.SCOPE_INVALID);
  }

  [Fact]
  public void ValidVerbPasses() {
    Should.NotThrow(() => DslScopeRules.EnsureValidScopes(typeof(ValidVerb)));
  }

  [Fact]
  public void VerbAttributeScopesAreValidated() {
    Should.Throw<WarpException>(() => DslScopeRules.EnsureValidScopes(typeof(InvalidAttributeScopeVerb)))
      .TargetObject.ShouldBe(typeof(SealedPublicScope));
  }

  [Fact]
  public void OpensScopeTargetsAreValidated() {
    Should.Throw<WarpException>(() => DslScopeRules.EnsureValidScopes(typeof(InvalidOpensScopeVerb)))
      .TargetObject.ShouldBe(typeof(OpenScope));
  }

  private abstract class AbstractScope : DslScope;

  private sealed class SealedPrivateScope : DslScope {
    private SealedPrivateScope() { }
  }

  private sealed class SealedPublicScope : DslScope;

  private class OpenScope : DslScope;

  private sealed class SealedInternalScope : DslScope {
    internal SealedInternalScope() { }
  }

  private abstract class GenericScope<T> : DslScope;

  [LoomVerb("Valid", typeof(DraftScope))]
  private sealed class ValidVerb : LoomVerb {
    [OpensScope(typeof(AbstractScope))]
    public ScriptBlock? Body { get; set; }

    protected override void Weave() { }
  }

  [LoomVerb("InvalidAttributeScope", typeof(DraftScope), typeof(SealedPublicScope))]
  private sealed class InvalidAttributeScopeVerb : LoomVerb {
    protected override void Weave() { }
  }

  [LoomVerb("InvalidOpensScope", typeof(DraftScope))]
  private sealed class InvalidOpensScopeVerb : LoomVerb {
    [OpensScope(typeof(OpenScope))]
    public ScriptBlock? Body { get; set; }

    protected override void Weave() { }
  }
}
