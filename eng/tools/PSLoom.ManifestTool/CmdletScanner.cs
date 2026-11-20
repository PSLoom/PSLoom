// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PSLoom.ManifestTool;

/// <summary>
///   Reads the cmdlet names of a compiled module from its metadata, without loading the assembly or its references.
/// </summary>
internal static class CmdletScanner {
  private const string ATTRIBUTE_NAME = "CmdletAttribute";
  private const string ATTRIBUTE_NAMESPACE = "System.Management.Automation";
  private const ushort ATTRIBUTE_PROLOG = 1;

  internal static IReadOnlyList<string> Scan(string assemblyPath) {
    using var stream = File.OpenRead(assemblyPath);
    using var portableExecutable = new PEReader(stream);
    var metadata = portableExecutable.GetMetadataReader();
    var names = new List<string>();

    foreach (var typeHandle in metadata.TypeDefinitions) {
      foreach (var attributeHandle in metadata.GetTypeDefinition(typeHandle).GetCustomAttributes()) {
        var attribute = metadata.GetCustomAttribute(attributeHandle);

        if (!IsCmdletAttribute(metadata, attribute)) {
          continue;
        }

        // The constructor is CmdletAttribute(string verbName, string nounName): a prolog and two strings.
        var blob = metadata.GetBlobReader(attribute.Value);

        if (blob.ReadUInt16() != ATTRIBUTE_PROLOG) {
          continue;
        }

        var verb = blob.ReadSerializedString();
        var noun = blob.ReadSerializedString();
        names.Add($"{verb}-{noun}");
      }
    }

    names.Sort(StringComparer.Ordinal);

    return names;
  }

  private static bool IsCmdletAttribute(MetadataReader metadata, CustomAttribute attribute) {
    if (attribute.Constructor.Kind != HandleKind.MemberReference) {
      return false;
    }

    var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);

    if (constructor.Parent.Kind != HandleKind.TypeReference) {
      return false;
    }

    var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);

    return metadata.StringComparer.Equals(type.Name, ATTRIBUTE_NAME) &&
           metadata.StringComparer.Equals(type.Namespace, ATTRIBUTE_NAMESPACE);
  }
}
