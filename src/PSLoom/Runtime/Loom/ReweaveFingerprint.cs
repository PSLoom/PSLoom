// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   Identity and fingerprint of a top-level verb invocation, from its bound parameters. The fingerprint is the first 128 bits of
///   SHA-256 over a canonical, order-independent serialization.
/// </summary>
internal static class ReweaveFingerprint {
  private const char SEPARATOR = '';

  public static UInt128 Compute(IReadOnlyDictionary<string, object?> boundParameters) {
    ArgumentNullException.ThrowIfNull(boundParameters);

    var builder = new StringBuilder();

    foreach (var name in boundParameters.Keys.Order(StringComparer.OrdinalIgnoreCase)) {
      builder.Append(name.ToUpperInvariant()).Append('=');
      Write(builder, boundParameters[name]);
      builder.Append(SEPARATOR);
    }

    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    return new UInt128(BitConverter.ToUInt64(hash, 0), BitConverter.ToUInt64(hash, 8));
  }

  /// <summary>
  ///   Builds the ledger key: the verb name plus its <c>[ReweaveKey]</c> values, or plus the fingerprint when it has none.
  /// </summary>
  public static string Key(string verbName, IReadOnlyList<string> keyParameters, IReadOnlyDictionary<string, object?> boundParameters,
  UInt128 fingerprint) {
    var builder = new StringBuilder(verbName);

    if (keyParameters.Count == 0) {
      return builder.Append(SEPARATOR).Append('#').Append(fingerprint.ToString("x32", CultureInfo.InvariantCulture)).ToString();
    }

    foreach (var parameter in keyParameters) {
      builder.Append(SEPARATOR);
      Write(builder, boundParameters.GetValueOrDefault(parameter));
    }

    return builder.ToString();
  }

  /// <summary>
  ///   Renders a recorded invocation for messages: the verb name followed by its <c>[ReweaveKey]</c> values.
  /// </summary>
  public static string Describe(LedgerItem item) {
    var values = item.Verb.ReweaveKeys
      .Select(parameter => item.Entry.BoundParameters.GetValueOrDefault(parameter))
      .Select(value => value switch {
        null => "$null",
        string text when text.Contains(' ') => $"'{text}'",
        var _ => LanguagePrimitives.ConvertTo<string>(value)
      });

    return string.Join(' ', [item.Entry.VerbName, .. values]);
  }

  private static void Write(StringBuilder builder, object? value) {
    while (true) {
      switch (value) {
        case null:
          builder.Append("null");
          break;
        case PSObject { BaseObject: PSCustomObject } custom:
          builder.Append('{');

          foreach (var property in custom.Properties.OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)) {
            builder.Append(property.Name.ToUpperInvariant()).Append(':');
            Write(builder, property.Value);
            builder.Append(SEPARATOR);
          }

          builder.Append('}');
          break;
        case PSObject wrapped:
          value = wrapped.BaseObject;
          continue;
        case string text:
          builder.Append('s').Append(text.Length).Append(':').Append(text);
          break;
        case SwitchParameter flag:
          builder.Append("b:").Append(flag.IsPresent);
          break;
        case bool boolean:
          builder.Append("b:").Append(boolean);
          break;
        case ScriptBlock script:
          var body = script.ToString();
          builder.Append("sb").Append(body.Length).Append(':').Append(body);
          break;
        case IDictionary dictionary:
          builder.Append('{');

          foreach (var key in dictionary.Keys.Cast<object>()
                     .OrderBy(key => Convert.ToString(key, CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)) {
            Write(builder, key);
            builder.Append(':');
            Write(builder, dictionary[key]);
            builder.Append(SEPARATOR);
          }

          builder.Append('}');
          break;
        case IEnumerable sequence:
          builder.Append('[');

          foreach (var item in sequence) {
            Write(builder, item);
            builder.Append(SEPARATOR);
          }

          builder.Append(']');
          break;
        case IFormattable formattable:
          builder.Append(value.GetType().Name).Append(':').Append(formattable.ToString(null, CultureInfo.InvariantCulture));
          break;
        default:
          builder.Append(value.GetType().FullName).Append(':').Append(LanguagePrimitives.ConvertTo<string>(value));
          break;
      }

      break;
    }
  }
}
