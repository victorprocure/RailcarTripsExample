using System.Buffers;
using System.Runtime.CompilerServices;

namespace RailcarTrips.Parser;

internal sealed class CsvParser
{
    const char BomChar = '\uFEFF';
    const char CarriageReturn = '\r';
    const char LineFeed = '\n';

    private readonly CsvOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvParser"/> class with optional parsing options.
    /// </summary>
    /// <param name="options">The parsing options; uses default options if not specified.</param>
    public CsvParser(CsvOptions? options = null)
    {
        _options = options ?? CsvOptions.Default;
    }

    /// <summary>
    /// Parses CSV data from a text reader and maps each row to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to map each row to.</typeparam>
    /// <param name="reader">The text reader containing CSV data.</param>
    /// <param name="map">The mapping function to transform each row.</param>
    /// <returns>An enumerable of mapped rows.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> or <paramref name="map"/> is null.</exception>
    public IEnumerable<T> Parse<T>(TextReader reader, Func<IReadOnlyList<string>, T> map)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(map);

        var skipFirst = true;
        foreach (var row in ReadRows(reader))
        {
            if (_options.SkipHeader && skipFirst)
            {
                skipFirst = false;
                continue;
            }

            if (row.All(field => string.IsNullOrWhiteSpace(field)))
            {
                continue;
            }

            yield return map(row);
        }
    }

    /// <summary>
    /// Asynchronously parses CSV data from a text reader and maps each row to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to map each row to.</typeparam>
    /// <param name="reader">The text reader containing CSV data.</param>
    /// <param name="map">The mapping function to transform each row.</param>
    /// <param name="cancellationToken">The cancellation token to observe during the asynchronous operation.</param>
    /// <returns>An asynchronous enumerable of mapped rows.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> or <paramref name="map"/> is null.</exception>
    public async IAsyncEnumerable<T> ParseAsync<T>(
        TextReader reader,
        Func<IReadOnlyList<string>, T> map,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(map);

        var skipFirst = true;
        await foreach (var row in ReadRowsAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            if (_options.SkipHeader && skipFirst)
            {
                skipFirst = false;
                continue;
            }

            if (row.All(field => string.IsNullOrWhiteSpace(field)))
            {
                continue;
            }

            yield return map(row);
        }
    }

    /// <remarks>
    /// I probably could've found a CSV parsing library that does this already, but
    /// I wanted to implement myself to flex a little and try something different.
    /// I wanted to see how memory efficient I could make it using pooled buffers.
    /// in the time I allotted myself.
    /// </remarks>
    private IEnumerable<string[]> ReadRows(TextReader reader)
    {

        var row = new List<string>(_options.InitialRowCapacity);
        var field = new FieldBuffer(_options.InitialFieldCapacity);

        var separator = _options.Separator;
        var quote = _options.Quote;
        var trimWhitespace = _options.TrimWhitespace;

        var inQuotes = false;
        var isFirstChar = true;

        var buffer = ArrayPool<char>.Shared.Rent(_options.BufferSize);

        try
        {
            while (true)
            {
                var read = reader.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    if (isFirstChar && row.Count == 0 && field.Length == 0)
                    {
                        yield break;
                    }

                    AddField(row, field, trimWhitespace);
                    if (row.Count > 0)
                    {
                        yield return row.ToArray();
                    }

                    yield break;
                }

                for (var i = 0; i < read; i++)
                {
                    var ch = buffer[i];

                    if (isFirstChar)
                    {
                        isFirstChar = false;
                        if (ch == BomChar)
                        {
                            continue;
                        }
                    }

                    if (inQuotes)
                    {
                        if (ch == quote)
                        {
                            char next;
                            if (i + 1 < read)
                            {
                                next = buffer[i + 1];
                                if (next == quote)
                                {
                                    field.Append(quote);
                                    i++;
                                }
                                else
                                {
                                    inQuotes = false;
                                }
                            }
                            else
                            {
                                var peek = reader.Peek();
                                if (peek == quote)
                                {
                                    reader.Read();
                                    field.Append(quote);
                                }
                                else
                                {
                                    inQuotes = false;
                                }
                            }
                        }
                        else
                        {
                            field.Append(ch);
                        }

                        continue;
                    }

                    if (ch == quote && field.Length == 0)
                    {
                        inQuotes = true;
                        continue;
                    }

                    if (ch == separator)
                    {
                        AddField(row, field, trimWhitespace);
                        continue;
                    }

                    if (ch == CarriageReturn)
                    {
                        if (i + 1 < read)
                        {
                            if (buffer[i + 1] == LineFeed)
                            {
                                i++;
                            }
                        }
                        else if (reader.Peek() == LineFeed)
                        {
                            reader.Read();
                        }

                        AddField(row, field, trimWhitespace);
                        yield return row.ToArray();
                        row.Clear();
                        continue;
                    }

                    if (ch == '\n')
                    {
                        AddField(row, field, trimWhitespace);
                        yield return row.ToArray();
                        row.Clear();
                        continue;
                    }

                    field.Append(ch);
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
            field.Dispose();
        }
    }

    /// <remarks>
    /// Same as above really, just the async variation
    /// </remarks>
    private async IAsyncEnumerable<string[]> ReadRowsAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var row = new List<string>(_options.InitialRowCapacity);
        var field = new FieldBuffer(_options.InitialFieldCapacity);

        var separator = _options.Separator;
        var quote = _options.Quote;
        var trimWhitespace = _options.TrimWhitespace;

        var inQuotes = false;
        var isFirstChar = true;

        var buffer = ArrayPool<char>.Shared.Rent(_options.BufferSize);
        var single = new char[1];

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    if (isFirstChar && row.Count == 0 && field.Length == 0)
                    {
                        yield break;
                    }

                    AddField(row, field, trimWhitespace);
                    if (row.Count > 0)
                    {
                        yield return row.ToArray();
                    }

                    yield break;
                }

                for (var i = 0; i < read; i++)
                {
                    var ch = buffer[i];

                    if (isFirstChar)
                    {
                        isFirstChar = false;
                        if (ch == BomChar)
                        {
                            continue;
                        }
                    }

                    if (inQuotes)
                    {
                        if (ch == quote)
                        {
                            if (i + 1 < read)
                            {
                                var next = buffer[i + 1];
                                if (next == quote)
                                {
                                    field.Append(quote);
                                    i++;
                                }
                                else
                                {
                                    inQuotes = false;
                                }
                            }
                            else
                            {
                                var peek = reader.Peek();
                                if (peek == quote)
                                {
                                    await reader.ReadAsync(single.AsMemory(0, 1), cancellationToken)
                                        .ConfigureAwait(false);
                                    field.Append(quote);
                                }
                                else
                                {
                                    inQuotes = false;
                                }
                            }
                        }
                        else
                        {
                            field.Append(ch);
                        }

                        continue;
                    }

                    if (ch == quote && field.Length == 0)
                    {
                        inQuotes = true;
                        continue;
                    }

                    if (ch == separator)
                    {
                        AddField(row, field, trimWhitespace);
                        continue;
                    }

                    if (ch == CarriageReturn)
                    {
                        if (i + 1 < read)
                        {
                            if (buffer[i + 1] == LineFeed)
                            {
                                i++;
                            }
                        }
                        else if (reader.Peek() == LineFeed)
                        {
                            await reader.ReadAsync(single.AsMemory(0, 1), cancellationToken)
                                .ConfigureAwait(false);
                        }

                        AddField(row, field, trimWhitespace);
                        yield return row.ToArray();
                        row.Clear();
                        continue;
                    }

                    if (ch == '\n')
                    {
                        AddField(row, field, trimWhitespace);
                        yield return row.ToArray();
                        row.Clear();
                        continue;
                    }

                    field.Append(ch);
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
            field.Dispose();
        }
    }

    private static void AddField(List<string> row, FieldBuffer field, bool trimWhitespace)
    {
        row.Add(field.ToStringAndClear(trimWhitespace));
    }

    /// <remarks>
    /// I could've added duck-typed enumerator struct in here for even more
    /// memory efficiency, but it was already getting a bit overkill.
    /// And taking up more time than I wanted.
    /// </remarks>
    private sealed class FieldBuffer : IDisposable
    {
        private char[] _buffer;
        private int _length;

        public FieldBuffer(int initialCapacity)
        {
            _buffer = ArrayPool<char>.Shared.Rent(initialCapacity);
        }

        public int Length => _length;

        public void Append(char value)
        {
            if (_length == _buffer.Length)
            {
                Grow();
            }

            _buffer[_length++] = value;
        }

        public string ToStringAndClear(bool trimWhitespace)
        {
            var len = _length;

            if (trimWhitespace && len > 0)
            {
                var start = 0;
                var end = len - 1;

                while (start < len && char.IsWhiteSpace(_buffer[start])) start++;
                while (end >= start && char.IsWhiteSpace(_buffer[end])) end--;

                len = end - start + 1;
                _length = 0;

                return len <= 0 ? string.Empty : new string(_buffer, start, len);
            }

            _length = 0;
            return len == 0 ? string.Empty : new string(_buffer, 0, len);
        }

        private void Grow()
        {
            var newBuffer = ArrayPool<char>.Shared.Rent(_buffer.Length * 2);
            _buffer.AsSpan(0, _length).CopyTo(newBuffer);
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }

        public void Dispose()
        {
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = Array.Empty<char>();
            _length = 0;
        }
    }
}

internal sealed record CsvOptions(char Separator, char Quote, bool TrimWhitespace, int BufferSize, int InitialFieldCapacity, int InitialRowCapacity, bool SkipHeader = true)
{
    public static CsvOptions Default { get; } = new(',', '"', TrimWhitespace: false, BufferSize: 32 * 1024, InitialFieldCapacity: 128, InitialRowCapacity: 16, SkipHeader: true);
}