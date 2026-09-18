// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Diagnostics;

namespace PSLoom.Runtime.Diagnostics;

/// <summary>
///   A bounded diagnostic log backed by a fixed array. When full, a new entry replaces the oldest one.
/// </summary>
/// <typeparam name="TEntry">The entry type.</typeparam>
internal sealed class RingBuffer<TEntry> : IDiagnosticLog<TEntry> where TEntry : class {
  internal const int DEFAULT_CAPACITY = 200;

  private readonly TEntry?[] _items;
  private readonly Lock _lock = new();
  private int _count;
  private int _head;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RingBuffer{TEntry}" /> class.
  /// </summary>
  /// <param name="capacity">The maximum number of entries kept.</param>
  public RingBuffer(int capacity = DEFAULT_CAPACITY) {
    ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
    _items = new TEntry?[capacity];
  }

  /// <inheritdoc />
  public int Capacity => _items.Length;

  /// <inheritdoc />
  public int Count {
    get {
      lock (_lock) {
        return _count;
      }
    }
  }

  /// <inheritdoc />
  public void Record(TEntry entry) {
    ArgumentNullException.ThrowIfNull(entry);

    lock (_lock) {
      _items[(_head + _count) % _items.Length] = entry;

      if (_count == _items.Length) {
        _head = (_head + 1) % _items.Length;
      }
      else {
        _count++;
      }
    }
  }

  /// <inheritdoc />
  public IReadOnlyList<TEntry> Snapshot(int? last = null) {
    lock (_lock) {
      var take = last is { } requested ? Math.Clamp(requested, 0, _count) : _count;

      if (take == 0) {
        return [];
      }

      var result = new TEntry[take];
      var start = _head + _count - take;

      for (var index = 0; index < take; index++) {
        result[index] = _items[(start + index) % _items.Length]!;
      }

      return result;
    }
  }

  /// <inheritdoc />
  public void Clear() {
    lock (_lock) {
      Array.Clear(_items);
      _head = 0;
      _count = 0;
    }
  }
}
