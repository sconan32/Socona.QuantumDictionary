using Socona.QuantumDictionary.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Socona.QuantumDictionary.Models
{
    internal class BTreeWordSearchRequest : WordSearchRequest
    {
        protected BTreeDictionaryBook dict;
        protected string queryString;
        protected long maxResults;
        protected int minLength;
        protected int maxSuffixVariation;
        protected bool allowMiddleMatches;
        protected bool isCancelled;
        protected bool hasExited;


        public BTreeWordSearchRequest(BTreeDictionaryBook dict, string queryString, int minLength,
            int maxSuffixVariation, bool allowMiddleMatches, long maxResults, bool startInstantly = true)
        {
            this.dict = dict;
            this.queryString = queryString;
            this.minLength = minLength;
            this.maxSuffixVariation = maxSuffixVariation;
            this.allowMiddleMatches = allowMiddleMatches;
            this.maxResults = maxResults;

            if (startInstantly)
            {
                Start();
            }
        }
        public void FindMatches()
        {
            bool useWildcards = false;
            if (allowMiddleMatches)
                useWildcards = queryString.Contains('*') ||
                                queryString.Contains('?') ||
                               queryString.Contains('[') ||
                                queryString.Contains(']');
            string folded = TextFolding.Apply(queryString);
            int minMatchLength = 0;

            if (useWildcards)
            {
                //#if QT_VERSION >= QT_VERSION_CHECK( 5, 0, 0 )
                //    regexp.setPattern( wildcardsToRegexp( gd::toQString( Folding::applyDiacriticsOnly( Folding::applySimpleCaseOnly( str ) ) ) ) );
                //    if( !regexp.isValid() )
                //      regexp.setPattern( QRegularExpression::escape( regexp.pattern() ) );
                //    regexp.setPatternOptions( QRegularExpression::CaseInsensitiveOption );
                //#else
                //                regexp.setPattern(gd::toQString(TextFolding.applyDiacriticsOnly(TextFolding.applySimpleCaseOnly(str))));
                //                regexp.setPatternSyntax(QRegExp::WildcardUnix);
                //                regexp.setCaseSensitivity(Qt::CaseInsensitive);
                //#endif

                bool bNoLetters = !folded.Any();
                string foldedWithWildcards;

                if (bNoLetters)
                    foldedWithWildcards = TextFolding.applyWhitespaceOnly(queryString);
                else
                    foldedWithWildcards = TextFolding.Apply(queryString, useWildcards);

                // Calculate minimum match length

                bool insideSet = false;
                bool escaped = false;
                for (int x = 0; x < foldedWithWildcards.Length; x++)
                {
                    char ch = foldedWithWildcards[x];

                    if (ch == '\\' && !escaped)
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == ']' && !escaped)
                    {
                        insideSet = false;
                        continue;
                    }

                    if (insideSet)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '[' && !escaped)
                    {
                        minMatchLength += 1;
                        insideSet = true;
                        continue;
                    }

                    if (ch == '*' && !escaped)
                        continue;

                    escaped = false;
                    minMatchLength += 1;
                }

                // Fill first match chars

                StringBuilder foldedBuilder = new StringBuilder(folded.Length);

                escaped = false;
                for (int x = 0; x < foldedWithWildcards.Length; x++)
                {
                    char ch = foldedWithWildcards[x];

                    if (escaped)
                    {
                        if (bNoLetters || (ch != '*' && ch != '?' && ch != '[' && ch != ']'))
                            foldedBuilder.Append(ch);
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        if (bNoLetters || !folded.Any())
                        {
                            escaped = true;
                            continue;
                        }
                        else
                            break;
                    }

                    if (ch == '*' || ch == '?' || ch == '[' || ch == ']')
                        break;

                    foldedBuilder.Append(ch);
                }
                folded = foldedBuilder.ToString();
            }
            else
            {
                if (!folded.Any())
                    folded = TextFolding.applyWhitespaceOnly(queryString);
            }

            int initialFoldedSize = folded.Length;

            int charsLeftToChop = 0;

            if (maxSuffixVariation >= 0)
            {
                charsLeftToChop = initialFoldedSize - (int)minLength;

                if (charsLeftToChop < 0)
                    charsLeftToChop = 0;
                else
                if (charsLeftToChop > maxSuffixVariation)
                    charsLeftToChop = maxSuffixVariation;
            }

            try
            {
                for (; ; )
                {
                    bool exactMatch;
                    List<char> leaf;
                    int nextLeaf;
                    char[] leafEnd;

                    char[] chainOffset = dict.findChainOffsetExactOrPrefix(folded, exactMatch,
                                                                                  leaf, nextLeaf,
                                                                                  leafEnd);

                    if (chainOffset)
                        for (; ; )
                        {
                            //if (Qt4x5::AtomicInt::loadAcquire(isCancelled))
                            //    break;

                            //DPRINTF( "offset = %u, size = %u\n", chainOffset - &leaf.front(), leaf.size() );

                            List<WordArticleLink> chain = dict.readChain(chainOffset);

                            string chainHead = Utf8::decode(chain[0].word);

                            string resultFolded = TextFolding.Apply(chainHead);
                            if (resultFolded.empty())
                                resultFolded = Folding::applyWhitespaceOnly(chainHead);

                            if ((useWildcards && folded.empty()) ||
                                 (resultFolded.size() >= folded.size()
                                   && !resultFolded.compare(0, folded.size(), folded)))
                            {
                                // Exact or prefix match

                                Mutex::Lock _(dataMutex );

                                for (unsigned x = 0; x < chain.size(); ++x)
                                {
                                    if (useWildcards)
                                    {
                                        string word = Utf8::decode(chain[x].prefix + chain[x].word);
                                        string result = Folding::applyDiacriticsOnly(word);
                                        //#if QT_VERSION >= QT_VERSION_CHECK( 5, 0, 0 )
                                        //              if( result.size() >= (wstring::size_type)minMatchLength )
                                        //              {
                                        //                QRegularExpressionMatch match = regexp.match( gd::toQString( result ) );
                                        //                if( match.hasMatch() && match.capturedStart() == 0 )
                                        //                {
                                        //                  addMatch( word );
                                        //                }
                                        //              }
                                        //#else
                                        //                            if (result.size() >= (wstring::size_type)minMatchLength
                                        //                                && regexp.indexIn(gd::toQString(result)) == 0
                                        //                                && regexp.matchedLength() >= minMatchLength)
                                        //                            {
                                        //                                addMatch(word);
                                        //                            }
                                        //#endif
                                    }
                                    else
                                    {
                                        // Skip middle matches, if requested. If suffix variation is specified,
                                        // make sure the string isn't larger than requested.
                                        if ((allowMiddleMatches || TextFolding.Apply(Utf8::decode(chain[x].prefix)).empty()) &&
                                             (maxSuffixVariation < 0 || (int)resultFolded.size() - initialFoldedSize <= maxSuffixVariation))
                                            addMatch(Utf8::decode(chain[x].prefix + chain[x].word));
                                    }
                                }

                                //if (Qt4x5::AtomicInt::loadAcquire(isCancelled))
                                //    break;

                                if (matches.size() >= maxResults)
                                {
                                    // For now we actually allow more than maxResults if the last
                                    // chain yield more than one result. That's ok and maybe even more
                                    // desirable.
                                    break;
                                }
                            }
                            else
                                // Neither exact nor a prefix match, end this
                                break;

                            // Fetch new leaf if we're out of chains here

                            if (chainOffset >= leafEnd)
                            {
                                // We're past the current leaf, fetch the next one

                                //DPRINTF( "advancing\n" );

                                if (nextLeaf)
                                {


                                    dict.readNode(nextLeaf, leaf);
                                    leafEnd = &leaf.front() + leaf.size();

                                    nextLeaf = dict.idxFile->read<uint32_t>();
                                    chainOffset = &leaf.front() + sizeof(uint32_t);

                                    int leafEntries = *(uint32_t*)&leaf.front();

                                    if (leafEntries == 0xffffFFFF)
                                    {
                                        //DPRINTF( "bah!\n" );
                                        exit(1);
                                    }
                                }
                                else
                                    break; // That was the last leaf
                            }
                        }

                    if (charsLeftToChop && !Qt4x5::AtomicInt::loadAcquire(isCancelled))
                    {
                        --charsLeftToChop;
                        folded.resize(folded.size() - 1);
                    }
                    else
                        break;
                }
            }
            catch (Exception e)
            {
                throw ("Index searching failed: \"%s\", error: %s\n",
                          dict.getName().c_str(), e.what());
            }

        }
        public void Start()
        {

        }

        public void Cancel()
        {

        }


    }
}
