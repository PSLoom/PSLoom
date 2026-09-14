namespace PSLoom.Warp.Dsl;

/// <summary>
///   Marks a parameter that is part of a verb invocation's identity for <c>Invoke-Loom -Reweave</c>
///   (e.g. <c>Style</c>'s context and name, <c>Sley</c>'s name).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ReweaveKeyAttribute : Attribute;
