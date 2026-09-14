namespace PSLoom.Warp.Styles;

/// <summary>
///   What a watcher observes.
/// </summary>
/// <param name="Context">The watched concrete context, or the written context for a pattern watcher.</param>
/// <param name="Name">The style name.</param>
/// <param name="OldValue">The previous value; <see langword="null" /> on replay or when nothing was defined.</param>
/// <param name="NewValue">The new value; <see langword="null" /> when the key no longer resolves.</param>
public readonly record struct StyleChange(string Context, string Name, object? OldValue, object? NewValue);