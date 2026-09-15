namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   The outcome of a module import.
/// </summary>
internal readonly record struct ModuleImportResult(ModuleImportStatus Status, Exception? Exception = null);