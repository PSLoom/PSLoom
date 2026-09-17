namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   How a module import ended.
/// </summary>
internal enum ModuleImportStatus {
  Imported,
  NotFound,
  Failed
}