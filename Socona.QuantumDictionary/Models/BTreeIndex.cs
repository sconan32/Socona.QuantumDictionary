using Socona.QuantumDictionary.Text;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Socona.QuantumDictionary.Models
{
    internal class BTreeIndex
    {
        private int indexNodeSize;
        private int rootOffset;
        bool rootNodeLoaded;
        Memory<char> rootNode;
        protected FileStream indexFile;

        public BTreeIndex()
        {

        }

        void openIndex(IndexInfo indexInfo, FileStream file)
        {
            indexNodeSize = indexInfo.btreeMaxElements;
            rootOffset = indexInfo.rootOffset;

            indexFile = file;
            // idxFileMutex = &mutex;

            rootNodeLoaded = false;

        }


        List<WordArticleLink> findArticles(string word, bool ignoreDiacritics)
        {
            List<WordArticleLink> result = new List<WordArticleLink>();

            try
            {
                string folded = TextFolding.Apply(word);
                if (string.IsNullOrEmpty(folded))
                    folded = TextFolding.applyWhitespaceOnly(word);

                bool exactMatch;

                List<char> leaf;
                int nextLeaf;

                char* leafEnd;

                char* chainOffset = findChainOffsetExactOrPrefix(folded, exactMatch,
                                                                         leaf, nextLeaf,
                                                                         leafEnd);

                if (chainOffset && exactMatch)
                {
                    result = readChain(chainOffset);

                    antialias(word, result, ignoreDiacritics);
                }
            }
            catch (Exception e)
            {
                gdWarning("Articles searching failed, error: %s\n", e.what());
                result.clear();
            }


            return result;
        }



        /// Reads a node or leaf at the given offset. Just uncompresses its data
        /// to the given vector and does nothing more.
        protected void readNode(int offset, ref Memory<char> output)
        {
            try
            {
                indexFile.Seek(offset, SeekOrigin.Begin);

                Span<byte> buffer = stackalloc byte[4];
                int count = indexFile.Read(buffer);
                if (count != 4)
                {
                    throw new InvalidDataException("Index File Invalid");
                }
                uint uncompressedSize = BitConverter.ToUInt32(buffer);

                count = indexFile.Read(buffer);
                if (count != 4)
                {
                    throw new InvalidDataException("Index File Invalid");
                }
                uint compressedSize = BitConverter.ToUInt32(buffer);

                Debug.WriteLine("%x,%x\n", uncompressedSize, compressedSize);

                using IMemoryOwner<char> owner = MemoryPool<char>.Shared.Rent((int)uncompressedSize);
                output = owner.Memory;

                Span<byte> compressedData = stackalloc byte[(int)compressedSize * sizeof(ushort)];
                count = indexFile.Read(compressedData);



                using MemoryStream<byte> compressedStream = new MemoryStream()
                using DeflateStream decompressor = new DeflateStream(ou CompressionMode.Decompress);




                ulong decompressedLength = output.size();

                if (uncompress((ushort*)&output.front(),
                       &decompressedLength,
                       &compressedData.front(),
                       compressedData.size()) != Z_OK ||
           decompressedLength != output.size())

                    throw exFailedToDecompressNode();
            }
            catch (Exception ex)
            {

            }

        }


        // Reads the word-article links' chain at the given offset. The pointer
        /// is updated to point to the next chain, if there's any.
        protected List<WordArticleLink> readChain(Span<char> output)
        {

        }

        /// Drops any aliases which arose due to folding. Only case-folded aliases
        /// are left.
        protected void antialias(string str, List<WordArticleLink> chain, bool ignoreDiacritics)
        {
            string caseFolded = TextFolding.applySimpleCaseOnly(str);
            if (ignoreDiacritics)
                caseFolded = TextFolding.applyDiacriticsOnly(caseFolded);

            for (int x = chain.Count; x-- > 0;)
            {
                // If after applying case folding to each word they wouldn't match, we
                // drop the entry.
                string entry = TextFolding.applySimpleCaseOnly((chain[x].prefix + chain[x].word).Normalize(NormalizationForm.FormC));
                if (ignoreDiacritics)
                    entry = TextFolding.applyDiacriticsOnly(entry);

                if (entry != caseFolded)
                    chain.RemoveAt(x);
                else
                if (chain[x].prefix.Length > 0) // If there's a prefix, merge it with the word,
                                                // since it's what dictionaries expect
                {
                    chain[x] = new WordArticleLink(chain[x].word.Insert(0, chain[x].prefix),
                        chain[x].articleOffset);
                }
            }

        }
        /// Finds the offset in the btree leaf for the given word, either matching
        /// by an exact match, or by finding the smallest entry that might match
        /// by prefix. It can return zero if there isn't even a possible prefx
        /// match. The input string must already be folded. The exactMatch is set
        /// to true when an exact match is located, and to false otherwise.
        /// The located leaf is loaded to 'leaf', and the pointer to the next
        /// leaf is saved to 'nextLeaf'.
        /// However, due to root node being permanently cached, the 'leaf' passed
        /// might not get used at all if the root node was the terminal one. In that
        /// case, the returned pointer wouldn't belong to 'leaf' at all. To that end,
        /// the leafEnd pointer always holds the pointer to the first byte outside
        /// the node data.
        public char[] findChainOffsetExactOrPrefix(string target, ref bool exactMatch,
                        ref Span<char> extLeaf, ref int nextLeaf, char[] leafEnd)
        {
            //    if (!idxFile)
            //        throw exIndexWasNotOpened();

            //  Mutex::Lock _( *idxFileMutex );

            // Lookup the index by traversing the index btree

            List<char> wcharBuffer;

            exactMatch = false;

            // Read a node

            int currentNodeOffset = rootOffset;

            if (!rootNodeLoaded)
            {
                // Time to load our root node. We do it only once, at the first request.
                readNode(rootOffset, ref rootNode);
                rootNodeLoaded = true;
            }

            char[] leaf = rootNode[0];
            leafEnd = leaf + rootNode.Count;

            if (!target.Any())
            {
                //For empty target string we return first chain in index
                for (; ; )
                {
                    uint leafEntries = *(uint*)leaf;

                    if (leafEntries == 0xffffFFFF)
                    {
                        // A node
                        currentNodeOffset = *((uint*)leaf + 1);
                        readNode(currentNodeOffset, extLeaf);
                        leaf = &extLeaf.front();
                        leafEnd = leaf + extLeaf.size();
                        nextLeaf = idxFile->read<uint32_t>();
                    }
                    else
                    {
                        // A leaf
                        if (currentNodeOffset == rootOffset)
                        {
                            // Only one leaf in index, there's no next leaf
                            nextLeaf = 0;
                        }
                        if (!leafEntries)
                            return 0;

                        return leaf + sizeof(uint);
                    }
                }
            }

            for (; ; )
            {
                // Is it a leaf or a node?

                uint leafEntries = *(uint*)leaf;

                if (leafEntries == 0xffffFFFF)
                {
                    // A node

                    //DPRINTF( "=>a node\n" );

                    uint* offsets = (uint*)leaf + 1;

                    char* ptr = leaf + sizeof(uint32_t) +
                                       (indexNodeSize + 1) * sizeof(uint32_t);

                    // ptr now points to a span of zero-separated strings, up to leafEnd.
                    // We find our match using a binary search.

                    char* closestString;

                    int compareResult;

                    char* window = ptr;
                    unsigned windowSize = leafEnd - ptr;

                    for (; ; )
                    {
                        // We boldly shoot in the middle of the whole mess, and then adjust
                        // to the beginning of the string that we've hit.
                        char* testPoint = window + windowSize / 2;

                        closestString = testPoint;

                        while (closestString > ptr && closestString[-1])
                            --closestString;

                        size_t wordSize = strlen(closestString);

                        if (wcharBuffer.size() <= wordSize)
                            wcharBuffer.resize(wordSize + 1);

                        long result = Utf8::decode(closestString, wordSize, &wcharBuffer.front());

                        if (result < 0)
                            throw Utf8::exCantDecode(closestString);

                        wcharBuffer[result] = 0;

                        //DPRINTF( "Checking against %s\n", closestString );

                        compareResult = target.compare(&wcharBuffer.front());

                        if (!compareResult)
                        {
                            // The target string matches the current one. Finish the search.
                            break;
                        }
                        if (compareResult < 0)
                        {
                            // The target string is smaller than the current one.
                            // Go to the left.
                            windowSize = closestString - window;

                            if (!windowSize)
                                break;
                        }
                        else
                        {
                            // The target string is larger than the current one.
                            // Go to the right.
                            windowSize -= (closestString - window) + wordSize + 1;
                            window = closestString + wordSize + 1;

                            if (!windowSize)
                                break;
                        }
                    }


                    Debug.WriteLine("The winner is %s, compareResult = %d\n", closestString, compareResult);

                    if (closestString != ptr)
                    {
                        char  * left = closestString - 1;

                        while (left != ptr && left[-1])
                            --left;

                        Debug.WriteLine("To the left: %s\n", left);
                    }
                    else
                        Debug.WriteLine("To the lest -- nothing\n");

                    char  * right = closestString + strlen(closestString) + 1;

                    if (right != leafEnd)
                    {
                        Debug.WriteLine("To the right: %s\n", right);
                    }
                    else
                        Debug.WriteLine("To the right -- nothing\n");


                    // Now, whatever the outcome (compareResult) is, we need to find
                    // entry number for the closestMatch string.

                    int entry = 0;

                    for (char* next = ptr; next != closestString;
                    next += strlen(next) + 1, ++entry) ;

                    // Ok, now check the outcome

                    if (!compareResult)
                    {
                        // The target string matches the one found.
                        // Go to the right, since it's there where we store such results.
                        currentNodeOffset = offsets[entry + 1];
                    }
                    if (compareResult < 0)
                    {
                        // The target string is smaller than the one found.
                        // Go to the left.
                        currentNodeOffset = offsets[entry];
                    }
                    else
                    {
                        // The target string is larger than the one found.
                        // Go to the right.
                        currentNodeOffset = offsets[entry + 1];
                    }

                    //DPRINTF( "reading node at %x\n", currentNodeOffset );
                    readNode(currentNodeOffset, extLeaf);
                    leaf = &extLeaf.front();
                    leafEnd = leaf + extLeaf.size();
                }
                else
                {
                    //DPRINTF( "=>a leaf\n" );
                    // A leaf

                    // If this leaf is the root, there's no next leaf, it just can't be.
                    // We do this check because the file's position indicator just won't
                    // be in the right place for root node anyway, since we precache it.
                    nextLeaf = (currentNodeOffset != rootOffset ? idxFile->read<uint32_t>() : 0);

                    if (!leafEntries)
                    {
                        // Empty leaf? This may only be possible for entirely empty trees only.
                        if (currentNodeOffset != rootOffset)
                            throw exCorruptedChainData();
                        else
                            return 0; // No match
                    }

                    // Build an array containing all chain pointers
                    char  * ptr = leaf + sizeof(int);

                    int chainSize;

                    List<char*> chainOffsets(leafEntries);

                    {
                        char const ** nextOffset = &chainOffsets.front();

                        while (leafEntries--)
                        {
                            *nextOffset++ = ptr;

                            memcpy(&chainSize, ptr, sizeof(uint32_t));

                            //DPRINTF( "%s + %s\n", ptr + sizeof( uint32_t ), ptr + sizeof( uint32_t ) + strlen( ptr + sizeof( uint32_t ) ) + 1 );

                            ptr += sizeof(uint32_t) + chainSize;
                        }
                    }

                    // Now do a binary search in it, aiming to find where our target
                    // string lands.

                    char const ** window = &chainOffsets.front();
                    unsigned windowSize = chainOffsets.size();

                    for (; ; )
                    {
                        //DPRINTF( "window = %u, ws = %u\n", window - &chainOffsets.front(), windowSize );

                        char const ** chainToCheck = window + windowSize / 2;
                        ptr = *chainToCheck;

                        memcpy(&chainSize, ptr, sizeof(uint32_t));
                        ptr += sizeof(uint32_t);

                        size_t wordSize = strlen(ptr);

                        if (wcharBuffer.size() <= wordSize)
                            wcharBuffer.resize(wordSize + 1);

                        //DPRINTF( "checking against word %s, left = %u\n", ptr, leafEntries );

                        long result = Utf8::decode(ptr, wordSize, &wcharBuffer.front());

                        if (result < 0)
                            throw Utf8::exCantDecode(ptr);

                        wcharBuffer[result] = 0;

                        wstring foldedWord = TextFolding.apply(&wcharBuffer.front());
                        if (foldedWord.empty())
                            foldedWord = TextFolding.applyWhitespaceOnly(&wcharBuffer.front());

                        int compareResult = target.compare(foldedWord);

                        if (!compareResult)
                        {
                            // Exact match -- return and be done
                            exactMatch = true;

                            return ptr - sizeof(int);
                        }
                        else
                        if (compareResult < 0)
                        {
                            // The target string is smaller than the current one.
                            // Go to the first half

                            windowSize /= 2;

                            if (!windowSize)
                            {
                                // That finishes our search. Since our target string
                                // landed before the last tested chain, we return a possible
                                // prefix match against that chain.
                                return ptr - sizeof(int);
                            }
                        }
                        else
                        {
                            // The target string is larger than the current one.
                            // Go to the second half

                            windowSize -= windowSize / 2 + 1;

                            if (!windowSize)
                            {
                                // That finishes our search. Since our target string
                                // landed after the last tested chain, we return the next
                                // chain. If there's no next chain in this leaf, this
                                // would mean the first element in the next leaf.
                                if (chainToCheck == &chainOffsets.back())
                                {
                                    if (nextLeaf)
                                    {
                                        readNode(nextLeaf, extLeaf);

                                        leafEnd = &extLeaf.front() + extLeaf.size();

                                        nextLeaf = idxFile->read<uint32_t>();

                                        return &extLeaf.front() + sizeof(uint32_t);
                                    }
                                    else
                                        return 0; // This was the last leaf
                                }
                                else
                                    return chainToCheck[1];
                            }

                            window = chainToCheck + 1;
                        }
                    }
                }
            }
        }
    }



    /// Information needed to open the index
    internal struct IndexInfo
    {
        public int btreeMaxElements { get; set; }
        public int rootOffset { get; set; }

        public IndexInfo(int btreeMaxElements, int rootOffset)
        {
            this.btreeMaxElements = btreeMaxElements;
            this.rootOffset = rootOffset;
        }
    }

    /// This structure describes a word linked to its translation. The
    /// translation is represented as an abstract 32-bit offset.
    internal struct WordArticleLink
    {
        public string word { get; set; }
        public string prefix { get; set; } // in utf8

        public int articleOffset { get; set; }

        public WordArticleLink() : this(string.Empty, 0)
        {

        }

        public WordArticleLink(string word, int articleOffset, string prefix = "")
        {
            this.word = word;
            this.prefix = prefix;
            this.articleOffset = articleOffset;
        }
    }

}
