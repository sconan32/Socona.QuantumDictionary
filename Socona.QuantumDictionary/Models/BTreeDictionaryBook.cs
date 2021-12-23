using Socona.QuantumDictionary.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Socona.QuantumDictionary.Models
{
    internal class BTreeDictionaryBook : BTreeIndex, IDictionaryBook
    {


        public string FtsIndexName { get; protected set; }

        public CancellationTokenSource FtsMutex { get; set; }

        public int FtsIndexVersion => 0;

        public bool IsLocalDictionary => true;

        public virtual WordSearchRequest PrefixMatch(string queryString, long maxResults)
        {
            return new BTreeWordSearchRequest(this, queryString, 0, -1, true, maxResults);
        }

        public virtual WordSearchRequest StemmedMatch(string queryString, int minLength, int maxSuffixVariation, long maxResults)
        {
            return new BTreeWordSearchRequest(this, queryString, minLength, maxSuffixVariation, false, maxResults);
        }

        public bool GetHeadWords(List<string> headwords)
        {

        }

        public string GetArticleText(int articleAddress, string headword)
        {

        }
    }

}
