// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
//
// This file is part of HLUTool.
//
// HLUTool is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// HLUTool is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with HLUTool.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HLU
{
    /// <summary>
    /// Provides extension methods for chunking sequences of items.
    /// </summary>
    public static class ChunkIt
    {
        #region Grouping extensions

        /// <summary>
        /// Groups contiguous elements in a sequence that share the same key, as defined by the keySelector function.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static IEnumerable<IGrouping<TKey, TSource>> ChunkBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return source.ChunkBy(keySelector, EqualityComparer<TKey>.Default);
        }

        /// <summary>
        /// Groups contiguous elements in a sequence that share the same key, as defined by the keySelector function and the provided equality comparer.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source"></param>
        /// <param name="keySelector"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static IEnumerable<IGrouping<TKey, TSource>> ChunkBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer)
        {
            // flag to signal end of source sequence.
            const bool noMoreSourceElements = true;

            // auto-generated iterator for the source array.
            var enumerator = source.GetEnumerator();

            // move to the first element in the source sequence.
            if (!enumerator.MoveNext()) yield break;

            // iterate through source sequence and create a copy of each Chunk
            // on each pass, the iterator advances to the first element of the next "Chunk"
            // in the source sequence. This loop corresponds to the outer foreach loop that
            // executes the query.
            Chunk<TKey, TSource> current = null;
            while (true)
            {
                // get the key for the current Chunk. The source iterator will churn through
                // the source sequence until it finds an element with a key that doesn't match.
                var key = keySelector(enumerator.Current);

                // make a new Chunk (group) object that initially has one GroupItem, which is a copy of the current source element.
                current = new Chunk<TKey, TSource>(key, enumerator, value => comparer.Equals(key, keySelector(value)));

                // return the Chunk. A Chunk is an IGrouping<TKey,TSource>, which is the return value of the ChunkBy method
                // at this point the Chunk only has the first element in its source sequence. The remaining elements will be
                // returned only when the client code foreach's over this chunk. See Chunk.GetEnumerator for more info
                yield return current;

                // check whether (a) the chunk has made a copy of all its source elements or
                // (b) the iterator has reached the end of the source sequence. If the caller uses an inner
                // foreach loop to iterate the chunk items, and that loop ran to completion,
                // then the Chunk.GetEnumerator method will already have made
                // copies of all chunk items before we get here. If the Chunk.GetEnumerator loop did not
                // enumerate all elements in the chunk, we need to do it here to avoid corrupting the iterator
                // for clients that may be calling us on a separate thread
                if (current.CopyAllChunkElements() == noMoreSourceElements)
                {
                    yield break;
                }
            }
        }

        #endregion Grouping extensions

        #region Chunking

        /// <summary>
        /// A Chunk is a contiguous group of one or more source elements that have the same key.
        /// A Chunk has a key and a list of ChunkItem objects, which are copies of the elements in the source sequence.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TSource"></typeparam>
        class Chunk<TKey, TSource> : IGrouping<TKey, TSource>
        {
            // INVARIANT: DoneCopyingChunk == true ||
            //   (predicate != null && predicate(enumerator.Current) && current.Value == enumerator.Current)

            /// <summary>
            /// A Chunk has a linked list of ChunkItems, which represent the elements in the current chunk.
            /// Each ChunkItem has a reference to the next ChunkItem in the list.
            /// </summary>
            class ChunkItem
            {
                public ChunkItem(TSource value)
                {
                    Value = value;
                }
                public readonly TSource Value;
                public ChunkItem Next = null;
            }

            /// <summary>
            /// The value that is used to determine matching elements.
            /// </summary>
            private readonly TKey key;

            /// <summary>
            /// Stores a reference to the enumerator for the source sequence.
            /// </summary>
            private IEnumerator<TSource> enumerator;

            /// <summary>
            /// A reference to the predicate that is used to compare keys.
            /// </summary>
            private Func<TSource, bool> predicate;

            /// <summary>
            /// Stores the contents of the first source element that belongs with this chunk.
            /// </summary>
            private readonly ChunkItem head;

            /// <summary>
            /// End of the list. It is repositioned each time a new ChunkItem is added.
            /// </summary>
            private ChunkItem tail;

            /// <summary>
            /// Flag to indicate the source iterator has reached the end of the source sequence.
            /// </summary>
            internal bool isLastSourceElement = false;

            /// <summary>
            /// Private object for thread syncronization.
            /// </summary>
            private object _lock;

            /// <summary>
            /// REQUIRES: enumerator != null && predicate != null
            /// </summary>
            /// <param name="key"></param>
            /// <param name="enumerator"></param>
            /// <param name="predicate"></param>
            public Chunk(TKey key, IEnumerator<TSource> enumerator, Func<TSource, bool> predicate)
            {
                this.key = key;
                this.enumerator = enumerator;
                this.predicate = predicate;

                // a Chunk always contains at least one element.
                head = new ChunkItem(enumerator.Current);

                // the end and beginning are the same until the list contains > 1 elements.
                tail = head;

                _lock = new object();
            }

            /// <summary>
            /// Indicates that all chunk elements have been copied to the list of ChunkItems,
            /// and the source enumerator is either at the end, or else on an element with a new key
            /// the tail of the linked list is set to null in the CopyNextChunkElement method if the
            /// key of the next element does not match the current chunk's key, or there are no more elements in the source.
            /// </summary>
            private bool DoneCopyingChunk { get { return tail == null; } }

            /// <summary>
            /// Adds one ChunkItem to the current group.
            /// REQUIRES: !DoneCopyingChunk && lock(this)
            /// </summary>
            private void CopyNextChunkElement()
            {
                // try to advance the iterator on the source sequence
                // if MoveNext returns false we are at the end, and isLastSourceElement is set to true
                isLastSourceElement = !enumerator.MoveNext();

                // if we are (a) at the end of the source, or (b) at the end of the current chunk
                // then null out the enumerator and predicate for reuse with the next chunk
                if (isLastSourceElement || !predicate(enumerator.Current))
                {
                    enumerator = null;
                    predicate = null;
                }
                else
                {
                    tail.Next = new ChunkItem(enumerator.Current);
                }

                // tail will be null if we are at the end of the chunk elements this check is made in DoneCopyingChunk
                tail = tail.Next;
            }

            /// <summary>
            /// Called after the end of the last chunk was reached. It first checks whether
            /// there are more elements in the source sequence. If there are, it
            /// returns true if enumerator for this chunk was exhausted.
            /// </summary>
            /// <returns></returns>
            internal bool CopyAllChunkElements()
            {
                while (true)
                {
                    lock (_lock)
                    {
                        if (DoneCopyingChunk)
                        {
                            // if isLastSourceElement is false, it signals to the outer iterator to continue iterating
                            return isLastSourceElement;
                        }
                        else
                        {
                            CopyNextChunkElement();
                        }
                    }
                }
            }

            /// <summary>
            /// The key for this chunk. All elements in the chunk share this key.
            /// </summary>
            public TKey Key { get { return key; } }

            /// <summary>
            /// Invoked by the inner foreach loop. This method stays just one step ahead
            /// of the client requests. It adds the next element of the chunk only after
            /// the clients requests the last element in the list so far.
            /// </summary>
            /// <returns></returns>
            public IEnumerator<TSource> GetEnumerator()
            {
                // specify the initial element to enumerate
                ChunkItem current = head;

                // there should always be at least one ChunkItem in a Chunk
                while (current != null)
                {
                    // yield the current item in the list
                    yield return current.Value;

                    // copy the next item from the source sequence,
                    // if we are at the end of our local list
                    lock (_lock)
                    {
                        if (current == tail)
                        {
                            CopyNextChunkElement();
                        }
                    }

                    // move to the next ChunkItem in the list
                    current = current.Next;
                }
            }

            /// <summary>
            /// Explicit non-generic enumerator implementation. It simply calls the generic version.
            /// </summary>
            /// <returns></returns>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        #endregion Chunking

        #region List extensions

        /// Break a list of items into chunks of a specific size.
        /// </summary>
        public static IEnumerable<List<T>> ChunkClause<T>(this IEnumerable<T> source, int chunksize)
        {
            // Check for invalid chunk size
            if (chunksize <= 0) throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunksize));

            while (source.Any())
            {
                yield return source.Take(chunksize).ToList();
                source = source.Skip(chunksize);
            }
        }

        /// <summary>
        /// Chunks a SQL filter clause into sub-lists, but only splits at top-level boundaries.
        /// </summary>
        /// <remarks>
        /// A split is only allowed when the current parenthesis depth is zero and the next condition's boolean operator is "OR".
        /// This prevents splitting paired conditions such as "incid &gt;= 1" AND "incid &lt;= 4", and prevents splitting inside parentheses.
        /// </remarks>
        /// <param name="source">The source conditions in their existing order.</param>
        /// <param name="chunkSize">
        /// The preferred minimum chunk size. Chunks may exceed this size if no safe split point occurs exactly at the size boundary.
        /// </param>
        /// <param name="hardMaxChunkSize">
        /// Optional hard maximum chunk size. If exceeded without encountering a safe split point, the method will split anyway.
        /// Use this to protect against SQL Server parameter limits.
        /// </param>
        /// <returns>An enumerable of chunked condition lists.</returns>
        public static IEnumerable<List<SqlFilterCondition>> ChunkClauseTopLevel(
            this IEnumerable<SqlFilterCondition> source,
            int chunkSize,
            int? hardMaxChunkSize = null)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (chunkSize <= 0)
                throw new ArgumentException("Chunk size must be greater than zero.", nameof(chunkSize));

            List<SqlFilterCondition> all = source as List<SqlFilterCondition> ?? source.ToList();
            if (all.Count == 0)
                yield break;

            List<SqlFilterCondition> chunk = [];
            int parenDepth = 0;

            for (int i = 0; i < all.Count; i++)
            {
                SqlFilterCondition cond = all[i];
                chunk.Add(cond);

                // Update parentheses depth after adding the current condition.
                parenDepth += CountChar(cond.OpenParentheses, '(');
                parenDepth -= CountChar(cond.CloseParentheses, ')');

                if (parenDepth < 0)
                {
                    // Defensive: the clause is malformed (more closing than opening).
                    // Reset to avoid cascading errors.
                    parenDepth = 0;
                }

                bool isLast = i == all.Count - 1;
                if (isLast)
                    continue;

                SqlFilterCondition next = all[i + 1];

                // Safe boundary: about to start a new top-level OR term.
                bool safeBoundary =
                    (parenDepth == 0) &&
                    next.BooleanOperator.Equals("OR", StringComparison.OrdinalIgnoreCase);

                // Preferred split: reached target size and we are at a safe boundary.
                if (safeBoundary && (chunk.Count >= chunkSize))
                {
                    yield return chunk;
                    chunk = [];
                    continue;
                }

                // Hard max protection: if requested, split even if not safe.
                if (hardMaxChunkSize.HasValue && (chunk.Count >= hardMaxChunkSize.Value))
                {
                    yield return chunk;
                    chunk = [];
                    continue;
                }
            }

            if (chunk.Count > 0)
                yield return chunk;
        }

        #endregion List extensions

        #region String extensions

        /// <summary>
        /// Counts the occurrences of a specific character in a string.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        private static int CountChar(string value, char c)
        {
            if (String.IsNullOrEmpty(value))
                return 0;

            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == c)
                    count++;
            }
            return count;
        }

        #endregion String extensions
    }
}