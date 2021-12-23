using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Socona.QuantumDictionary.Text
{
    public static partial class TextFolding
    {
        /// Tests if the given char is one of the Unicode combining marks. Some are
        /// caught by the diacritics folding table, but they are only handled there
        /// when they come with their main characters, not by themselves. The rest
        /// are caught here.
        public static bool IsCombiningMark(char ch)
        {
            return (
                     (ch >= 0x300 && ch <= 0x36F) ||
                     (ch >= 0x1DC0 && ch <= 0x1DFF) ||
                     (ch >= 0x20D0 && ch <= 0x20FF) ||
                     (ch >= 0xFE20 && ch <= 0xFE2F)
                   );
        }
        public static char foldedDiacritic(char[] inputString, int size, ref int consumed)
        {
            return foldDiacritic(inputString, size, ref consumed);
        }

        public static string Apply(string inputString, bool preserveWildcards = false)
        {
            // First, strip diacritics and apply ws/punctuation removal

            StringBuilder withoutDiacritics = new StringBuilder(inputString.Length);

            int consumed = 0;

            ReadOnlySpan<char> inputSpan = inputString.AsSpan();
            for (int left = inputString.Length; left > 0;)
            {
                var candidates = inputSpan[consumed..Math.Min(consumed + foldDiacriticMaxIn, inputString.Length)];
                char ch = foldDiacritic(candidates, left, ref consumed);

                if (!IsCombiningMark(ch) && !IsWhitespace(ch)
                     && (!isPunct(ch)
                          || (preserveWildcards &&
                               (ch == '\\' || ch == '?' || ch == '*' || ch == '[' || ch == ']'))
                        )
                    )
                    withoutDiacritics.Append(ch);

                left -= consumed;
            }

            // Now, fold the case            
            string withoutDiacriticsString = withoutDiacritics.ToString();
            StringBuilder caseFolded = new StringBuilder(withoutDiacriticsString.Length);

            Span<Rune> buf = stackalloc Rune[foldCaseMaxOut];

            foreach (var rune in withoutDiacriticsString.EnumerateRunes())
            {
                caseFolded.Append(buf[0..foldCase(rune, buf)].ToString());
            }

            return caseFolded.ToString();
        }

        public static string applySimpleCaseOnly(string inputString)
        {
            StringBuilder output = new StringBuilder(inputString.Length);

            foreach (var rune in inputString.EnumerateRunes())
            {
                output.Append(foldCaseSimple(rune));
            }
            return output.ToString();
        }

        public static string applyFullCaseOnly(string inputString)
        {

            StringBuilder caseFolded = new StringBuilder(inputString.Length);

            Span<Rune> buf = stackalloc Rune[foldCaseMaxOut];

            foreach (var rune in inputString.EnumerateRunes())
            {
                caseFolded.Append(buf[0..foldCase(rune, buf)].ToString());
            }

            return caseFolded.ToString();
        }

        public static string applyDiacriticsOnly(string inputString)
        {
            StringBuilder withoutDiacritics = new StringBuilder();
            int consumed = 0;

            ReadOnlySpan<char> inputSpan = inputString.AsSpan();

            for (int left = inputString.Length; left > 0;)
            {
                var candidates = inputSpan[consumed..Math.Min(consumed + foldDiacriticMaxIn, inputString.Length)];
                char ch = foldDiacritic(candidates, left, ref consumed);

                if (!IsCombiningMark(ch))
                    withoutDiacritics.Append(ch);
                left -= consumed;
            }

            return withoutDiacritics.ToString();
        }

        public static string applyPunctOnly(string inputString)
        {
            StringBuilder outputString = new StringBuilder();

            foreach (var ch in inputString)
                if (!isPunct(ch))
                    outputString.Append(ch);

            return outputString.ToString();
        }

        public static string applyWhitespaceOnly(string inputString)
        {
            StringBuilder outputString = new StringBuilder();
            foreach (var ch in inputString)
                if (!IsWhitespace(ch))
                    outputString.Append(ch);
            return outputString.ToString();
        }

        public static string applyWhitespaceAndPunctOnly(string inputString)
        {
            StringBuilder outputString = new StringBuilder();
            foreach (var ch in inputString)
                if (!IsWhitespace(ch) && !isPunct(ch))
                    outputString.Append(ch);

            return outputString.ToString();
        }

        public static bool IsWhitespace(char ch)
        {
            switch (ch)
            {
                case '\n':
                case '\r':
                case '\t':

                case (char)0x2028: // Zl, LINE SEPARATOR

                case (char)0x2029: // Zp, PARAGRAPH SEPARATOR

                case (char)0x0020: // Zs, SPACE
                case (char)0x00A0: // Zs, NO-BREAK SPACE
                case (char)0x1680: // Zs, OGHAM SPACE MARK
                case (char)0x180E: // Zs, MONGOLIAN VOWEL SEPARATOR
                case (char)0x2000: // Zs, EN QUAD
                case (char)0x2001: // Zs, EM QUAD
                case (char)0x2002: // Zs, EN SPACE
                case (char)0x2003: // Zs, EM SPACE
                case (char)0x2004: // Zs, THREE-PER-EM SPACE
                case (char)0x2005: // Zs, FOUR-PER-EM SPACE
                case (char)0x2006: // Zs, SIX-PER-EM SPACE
                case (char)0x2007: // Zs, FIGURE SPACE
                case (char)0x2008: // Zs, PUNCTUATION SPACE
                case (char)0x2009: // Zs, THIN SPACE
                case (char)0x200A: // Zs, HAIR SPACE
                case (char)0x202F: // Zs, NARROW NO-BREAK SPACE
                case (char)0x205F: // Zs, MEDIUM MATHEMATICAL SPACE
                case (char)0x3000: // Zs, IDEOGRAPHIC SPACE
                    return true;

                default:
                    return false;
            }
        }

        public static bool isPunct(char ch)
        {
            switch (ch)
            {
                // Pc

                case (char)0x005F: // LOW LINE
                case (char)0x203F: // UNDERTIE
                case (char)0x2040: // CHARACTER TIE
                case (char)0x2054: // INVERTED UNDERTIE
                case (char)0x30FB: // KATAKANA MIDDLE DOT
                case (char)0xFE33: // PRESENTATION FORM FOR VERTICAL LOW LINE
                case (char)0xFE34: // PRESENTATION FORM FOR VERTICAL WAVY LOW LINE
                case (char)0xFE4D: // DASHED LOW LINE
                case (char)0xFE4E: // CENTRELINE LOW LINE
                case (char)0xFE4F: // WAVY LOW LINE
                case (char)0xFF3F: // FULLWIDTH LOW LINE
                case (char)0xFF65: // HALFWIDTH KATAKANA MIDDLE DOT

                // Pd
                case (char)0x002D: // HYPHEN-MINUS
                case (char)0x058A: // ARMENIAN HYPHEN
                case (char)0x1806: // MONGOLIAN TODO SOFT HYPHEN
                case (char)0x2010: // HYPHEN
                case (char)0x2011: // NON-BREAKING HYPHEN
                case (char)0x2012: // FIGURE DASH
                case (char)0x2013: // EN DASH
                case (char)0x2014: // EM DASH
                case (char)0x2015: // HORIZONTAL BAR
                case (char)0x301C: // WAVE DASH
                case (char)0x3030: // WAVY DASH
                case (char)0x30A0: // KATAKANA-HIRAGANA DOUBLE HYPHEN
                case (char)0xFE31: // PRESENTATION FORM FOR VERTICAL EM DASH
                case (char)0xFE32: // PRESENTATION FORM FOR VERTICAL EN DASH
                case (char)0xFE58: // SMALL EM DASH
                case (char)0xFE63: // SMALL HYPHEN-MINUS
                case (char)0xFF0D: // FULLWIDTH HYPHEN-MINUS

                // Ps
                case (char)0x0028: // LEFT PARENTHESIS
                case (char)0x005B: // LEFT SQUARE BRACKET
                case (char)0x007B: // LEFT CURLY BRACKET
                case (char)0x0F3A: // TIBETAN MARK GUG RTAGS GYON
                case (char)0x0F3C: // TIBETAN MARK ANG KHANG GYON
                case (char)0x169B: // OGHAM FEATHER MARK
                case (char)0x201A: // SINGLE LOW-9 QUOTATION MARK
                case (char)0x201E: // DOUBLE LOW-9 QUOTATION MARK
                case (char)0x2045: // LEFT SQUARE BRACKET WITH QUILL
                case (char)0x207D: // SUPERSCRIPT LEFT PARENTHESIS
                case (char)0x208D: // SUBSCRIPT LEFT PARENTHESIS
                case (char)0x2329: // LEFT-POINTING ANGLE BRACKET
                case (char)0x2768: // MEDIUM LEFT PARENTHESIS ORNAMENT
                case (char)0x276A: // MEDIUM FLATTENED LEFT PARENTHESIS ORNAMENT
                case (char)0x276C: // MEDIUM LEFT-POINTING ANGLE BRACKET ORNAMENT
                case (char)0x276E: // HEAVY LEFT-POINTING ANGLE QUOTATION MARK ORNAMENT
                case (char)0x2770: // HEAVY LEFT-POINTING ANGLE BRACKET ORNAMENT
                case (char)0x2772: // LIGHT LEFT TORTOISE SHELL BRACKET ORNAMENT
                case (char)0x2774: // MEDIUM LEFT CURLY BRACKET ORNAMENT
                case (char)0x27C5: // LEFT S-SHAPED BAG DELIMITER
                case (char)0x27E6: // MATHEMATICAL LEFT WHITE SQUARE BRACKET
                case (char)0x27E8: // MATHEMATICAL LEFT ANGLE BRACKET
                case (char)0x27EA: // MATHEMATICAL LEFT DOUBLE ANGLE BRACKET
                case (char)0x27EC: // MATHEMATICAL LEFT WHITE TORTOISE SHELL BRACKET
                case (char)0x27EE: // MATHEMATICAL LEFT FLATTENED PARENTHESIS
                case (char)0x2983: // LEFT WHITE CURLY BRACKET
                case (char)0x2985: // LEFT WHITE PARENTHESIS
                case (char)0x2987: // Z NOTATION LEFT IMAGE BRACKET
                case (char)0x2989: // Z NOTATION LEFT BINDING BRACKET
                case (char)0x298B: // LEFT SQUARE BRACKET WITH UNDERBAR
                case (char)0x298D: // LEFT SQUARE BRACKET WITH TICK inputString TOP CORNER
                case (char)0x298F: // LEFT SQUARE BRACKET WITH TICK inputString BOTTOM CORNER
                case (char)0x2991: // LEFT ANGLE BRACKET WITH DOT
                case (char)0x2993: // LEFT ARC LESS-THAN BRACKET
                case (char)0x2995: // DOUBLE LEFT ARC GREATER-THAN BRACKET
                case (char)0x2997: // LEFT BLACK TORTOISE SHELL BRACKET
                case (char)0x29D8: // LEFT WIGGLY FENCE
                case (char)0x29DA: // LEFT DOUBLE WIGGLY FENCE
                case (char)0x29FC: // LEFT-POINTING CURVED ANGLE BRACKET
                case (char)0x2E22: // TOP LEFT HALF BRACKET
                case (char)0x2E24: // BOTTOM LEFT HALF BRACKET
                case (char)0x2E26: // LEFT SIDEWAYS U BRACKET
                case (char)0x2E28: // LEFT DOUBLE PARENTHESIS
                case (char)0x3008: // LEFT ANGLE BRACKET
                case (char)0x300A: // LEFT DOUBLE ANGLE BRACKET
                case (char)0x300C: // LEFT CORNER BRACKET
                case (char)0x300E: // LEFT WHITE CORNER BRACKET
                case (char)0x3010: // LEFT BLACK LENTICULAR BRACKET
                case (char)0x3014: // LEFT TORTOISE SHELL BRACKET
                case (char)0x3016: // LEFT WHITE LENTICULAR BRACKET
                case (char)0x3018: // LEFT WHITE TORTOISE SHELL BRACKET
                case (char)0x301A: // LEFT WHITE SQUARE BRACKET
                case (char)0x301D: // REVERSED DOUBLE PRIME QUOTATION MARK
                case (char)0xFD3E: // ORNATE LEFT PARENTHESIS
                case (char)0xFE17: // PRESENTATION FORM FOR VERTICAL LEFT WHITE LENTICULAR BRACKET
                case (char)0xFE35: // PRESENTATION FORM FOR VERTICAL LEFT PARENTHESIS
                case (char)0xFE37: // PRESENTATION FORM FOR VERTICAL LEFT CURLY BRACKET
                case (char)0xFE39: // PRESENTATION FORM FOR VERTICAL LEFT TORTOISE SHELL BRACKET
                case (char)0xFE3B: // PRESENTATION FORM FOR VERTICAL LEFT BLACK LENTICULAR BRACKET
                case (char)0xFE3D: // PRESENTATION FORM FOR VERTICAL LEFT DOUBLE ANGLE BRACKET
                case (char)0xFE3F: // PRESENTATION FORM FOR VERTICAL LEFT ANGLE BRACKET
                case (char)0xFE41: // PRESENTATION FORM FOR VERTICAL LEFT CORNER BRACKET
                case (char)0xFE43: // PRESENTATION FORM FOR VERTICAL LEFT WHITE CORNER BRACKET
                case (char)0xFE47: // PRESENTATION FORM FOR VERTICAL LEFT SQUARE BRACKET
                case (char)0xFE59: // SMALL LEFT PARENTHESIS
                case (char)0xFE5B: // SMALL LEFT CURLY BRACKET
                case (char)0xFE5D: // SMALL LEFT TORTOISE SHELL BRACKET
                case (char)0xFF08: // FULLWIDTH LEFT PARENTHESIS
                case (char)0xFF3B: // FULLWIDTH LEFT SQUARE BRACKET
                case (char)0xFF5B: // FULLWIDTH LEFT CURLY BRACKET
                case (char)0xFF5F: // FULLWIDTH LEFT WHITE PARENTHESIS
                case (char)0xFF62: // HALFWIDTH LEFT CORNER BRACKET

                // Pe
                case (char)0x0029: // RIGHT PARENTHESIS
                case (char)0x005D: // RIGHT SQUARE BRACKET
                case (char)0x007D: // RIGHT CURLY BRACKET
                case (char)0x0F3B: // TIBETAN MARK GUG RTAGS GYAS
                case (char)0x0F3D: // TIBETAN MARK ANG KHANG GYAS
                case (char)0x169C: // OGHAM REVERSED FEATHER MARK
                case (char)0x2046: // RIGHT SQUARE BRACKET WITH QUILL
                case (char)0x207E: // SUPERSCRIPT RIGHT PARENTHESIS
                case (char)0x208E: // SUBSCRIPT RIGHT PARENTHESIS
                case (char)0x232A: // RIGHT-POINTING ANGLE BRACKET
                case (char)0x23B5: // BOTTOM SQUARE BRACKET
                case (char)0x2769: // MEDIUM RIGHT PARENTHESIS ORNAMENT
                case (char)0x276B: // MEDIUM FLATTENED RIGHT PARENTHESIS ORNAMENT
                case (char)0x276D: // MEDIUM RIGHT-POINTING ANGLE BRACKET ORNAMENT
                case (char)0x276F: // HEAVY RIGHT-POINTING ANGLE QUOTATION MARK ORNAMENT
                case (char)0x2771: // HEAVY RIGHT-POINTING ANGLE BRACKET ORNAMENT
                case (char)0x2773: // LIGHT RIGHT TORTOISE SHELL BRACKET ORNAMENT
                case (char)0x2775: // MEDIUM RIGHT CURLY BRACKET ORNAMENT
                case (char)0x27E7: // MATHEMATICAL RIGHT WHITE SQUARE BRACKET
                case (char)0x27E9: // MATHEMATICAL RIGHT ANGLE BRACKET
                case (char)0x27EB: // MATHEMATICAL RIGHT DOUBLE ANGLE BRACKET
                case (char)0x2984: // RIGHT WHITE CURLY BRACKET
                case (char)0x2986: // RIGHT WHITE PARENTHESIS
                case (char)0x2988: // Z NOTATION RIGHT IMAGE BRACKET
                case (char)0x298A: // Z NOTATION RIGHT BINDING BRACKET
                case (char)0x298C: // RIGHT SQUARE BRACKET WITH UNDERBAR
                case (char)0x298E: // RIGHT SQUARE BRACKET WITH TICK inputString BOTTOM CORNER
                case (char)0x2990: // RIGHT SQUARE BRACKET WITH TICK inputString TOP CORNER
                case (char)0x2992: // RIGHT ANGLE BRACKET WITH DOT
                case (char)0x2994: // RIGHT ARC GREATER-THAN BRACKET
                case (char)0x2996: // DOUBLE RIGHT ARC LESS-THAN BRACKET
                case (char)0x2998: // RIGHT BLACK TORTOISE SHELL BRACKET
                case (char)0x29D9: // RIGHT WIGGLY FENCE
                case (char)0x29DB: // RIGHT DOUBLE WIGGLY FENCE
                case (char)0x29FD: // RIGHT-POINTING CURVED ANGLE BRACKET
                case (char)0x3009: // RIGHT ANGLE BRACKET
                case (char)0x300B: // RIGHT DOUBLE ANGLE BRACKET
                case (char)0x300D: // RIGHT CORNER BRACKET
                case (char)0x300F: // RIGHT WHITE CORNER BRACKET
                case (char)0x3011: // RIGHT BLACK LENTICULAR BRACKET
                case (char)0x3015: // RIGHT TORTOISE SHELL BRACKET
                case (char)0x3017: // RIGHT WHITE LENTICULAR BRACKET
                case (char)0x3019: // RIGHT WHITE TORTOISE SHELL BRACKET
                case (char)0x301B: // RIGHT WHITE SQUARE BRACKET
                case (char)0x301E: // DOUBLE PRIME QUOTATION MARK
                case (char)0x301F: // LOW DOUBLE PRIME QUOTATION MARK
                case (char)0xFD3F: // ORNATE RIGHT PARENTHESIS
                case (char)0xFE36: // PRESENTATION FORM FOR VERTICAL RIGHT PARENTHESIS
                case (char)0xFE38: // PRESENTATION FORM FOR VERTICAL RIGHT CURLY BRACKET
                case (char)0xFE3A: // PRESENTATION FORM FOR VERTICAL RIGHT TORTOISE SHELL BRACKET
                case (char)0xFE3C: // PRESENTATION FORM FOR VERTICAL RIGHT BLACK LENTICULAR BRACKET
                case (char)0xFE3E: // PRESENTATION FORM FOR VERTICAL RIGHT DOUBLE ANGLE BRACKET
                case (char)0xFE40: // PRESENTATION FORM FOR VERTICAL RIGHT ANGLE BRACKET
                case (char)0xFE42: // PRESENTATION FORM FOR VERTICAL RIGHT CORNER BRACKET
                case (char)0xFE44: // PRESENTATION FORM FOR VERTICAL RIGHT WHITE CORNER BRACKET
                case (char)0xFE48: // PRESENTATION FORM FOR VERTICAL RIGHT SQUARE BRACKET
                case (char)0xFE5A: // SMALL RIGHT PARENTHESIS
                case (char)0xFE5C: // SMALL RIGHT CURLY BRACKET
                case (char)0xFE5E: // SMALL RIGHT TORTOISE SHELL BRACKET
                case (char)0xFF09: // FULLWIDTH RIGHT PARENTHESIS
                case (char)0xFF3D: // FULLWIDTH RIGHT SQUARE BRACKET
                case (char)0xFF5D: // FULLWIDTH RIGHT CURLY BRACKET
                case (char)0xFF60: // FULLWIDTH RIGHT WHITE PARENTHESIS
                case (char)0xFF63: // HALFWIDTH RIGHT CORNER BRACKET

                // Pf
                case (char)0x00BB: // RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK
                case (char)0x2019: // RIGHT SINGLE QUOTATION MARK
                case (char)0x201D: // RIGHT DOUBLE QUOTATION MARK
                case (char)0x203A: // SINGLE RIGHT-POINTING ANGLE QUOTATION MARK


                // Pi
                case (char)0x00AB: // LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
                case (char)0x2018: // LEFT SINGLE QUOTATION MARK
                case (char)0x201C: // LEFT DOUBLE QUOTATION MARK
                case (char)0x2039: // SINGLE LEFT-POINTING ANGLE QUOTATION MARK

                // Po
                case (char)0x0021: // EXCLAMATION MARK
                case (char)0x0022: // QUOTATION MARK
                case (char)0x0023: // NUMBER SIGN
                case (char)0x0025: // PERCENT SIGN
                case (char)0x0026: // AMPERSAND
                case (char)0x0027: // APOSTROPHE
                case (char)0x002A: // ASTERISK
                case (char)0x002C: // COMMA
                case (char)0x002E: // FULL STOP
                case (char)0x002F: // SOLIDUS
                case (char)0x003A: // COLON
                case (char)0x003B: // SEMICOLON
                case (char)0x003F: // QUESTION MARK
                case (char)0x0040: // COMMERCIAL AT
                case (char)0x005C: // REVERSE SOLIDUS
                case (char)0x00A1: // INVERTED EXCLAMATION MARK
                case (char)0x00B7: // MIDDLE DOT
                case (char)0x00BF: // INVERTED QUESTION MARK
                case (char)0x037E: // GREEK QUESTION MARK
                case (char)0x0387: // GREEK ANO TELEIA
                case (char)0x055A: // ARMENIAN APOSTROPHE
                case (char)0x055B: // ARMENIAN EMPHASIS MARK
                case (char)0x055C: // ARMENIAN EXCLAMATION MARK
                case (char)0x055D: // ARMENIAN COMMA
                case (char)0x055E: // ARMENIAN QUESTION MARK
                case (char)0x055F: // ARMENIAN ABBREVIATION MARK
                case (char)0x0589: // ARMENIAN FULL STOP
                case (char)0x05BE: // HEBREW PUNCTUATION MAQAF
                case (char)0x05C0: // HEBREW PUNCTUATION PASEQ
                case (char)0x05C3: // HEBREW PUNCTUATION SOF PASUQ
                case (char)0x05F3: // HEBREW PUNCTUATION GERESH
                case (char)0x05F4: // HEBREW PUNCTUATION GERSHAYIM
                case (char)0x060C: // ARABIC COMMA
                case (char)0x060D: // ARABIC DATE SEPARATOR
                case (char)0x061B: // ARABIC SEMICOLON
                case (char)0x061F: // ARABIC QUESTION MARK
                case (char)0x066A: // ARABIC PERCENT SIGN
                case (char)0x066B: // ARABIC DECIMAL SEPARATOR
                case (char)0x066C: // ARABIC THOUSANDS SEPARATOR
                case (char)0x066D: // ARABIC FIVE POINTED STAR
                case (char)0x06D4: // ARABIC FULL STOP
                case (char)0x0700: // SYRIAC END OF PARAGRAPH
                case (char)0x0701: // SYRIAC SUPRALINEAR FULL STOP
                case (char)0x0702: // SYRIAC SUBLINEAR FULL STOP
                case (char)0x0703: // SYRIAC SUPRALINEAR COLON
                case (char)0x0704: // SYRIAC SUBLINEAR COLON
                case (char)0x0705: // SYRIAC HORIZONTAL COLON
                case (char)0x0706: // SYRIAC COLON SKEWED LEFT
                case (char)0x0707: // SYRIAC COLON SKEWED RIGHT
                case (char)0x0708: // SYRIAC SUPRALINEAR COLON SKEWED LEFT
                case (char)0x0709: // SYRIAC SUBLINEAR COLON SKEWED RIGHT
                case (char)0x070A: // SYRIAC CONTRACTION
                case (char)0x070B: // SYRIAC HARKLEAN OBELUS
                case (char)0x070C: // SYRIAC HARKLEAN METOBELUS
                case (char)0x070D: // SYRIAC HARKLEAN ASTERISCUS
                case (char)0x0964: // DEVANAGARI DANDA
                case (char)0x0965: // DEVANAGARI DOUBLE DANDA
                case (char)0x0970: // DEVANAGARI ABBREVIATION SIGN
                case (char)0x0DF4: // SINHALA PUNCTUATION KUNDDALIYA
                case (char)0x0E4F: // THAI CHARACTER FONGMAN
                case (char)0x0E5A: // THAI CHARACTER ANGKHANKHU
                case (char)0x0E5B: // THAI CHARACTER KHOMUT
                case (char)0x0F04: // TIBETAN MARK INITIAL YIG MGO MDUN MA
                case (char)0x0F05: // TIBETAN MARK CLOSING YIG MGO SGAB MA
                case (char)0x0F06: // TIBETAN MARK CARET YIG MGO PHUR SHAD MA
                case (char)0x0F07: // TIBETAN MARK YIG MGO TSHEG SHAD MA
                case (char)0x0F08: // TIBETAN MARK SBRUL SHAD
                case (char)0x0F09: // TIBETAN MARK BSKUR YIG MGO
                case (char)0x0F0A: // TIBETAN MARK BKA- SHOG YIG MGO
                case (char)0x0F0B: // TIBETAN MARK INTERSYLLABIC TSHEG
                case (char)0x0F0C: // TIBETAN MARK DELIMITER TSHEG BSTAR
                case (char)0x0F0D: // TIBETAN MARK SHAD
                case (char)0x0F0E: // TIBETAN MARK NYIS SHAD
                case (char)0x0F0F: // TIBETAN MARK TSHEG SHAD
                case (char)0x0F10: // TIBETAN MARK NYIS TSHEG SHAD
                case (char)0x0F11: // TIBETAN MARK RIN CHEN SPUNGS SHAD
                case (char)0x0F12: // TIBETAN MARK RGYA GRAM SHAD
                case (char)0x0F85: // TIBETAN MARK PALUTA
                case (char)0x104A: // MYANMAR SIGN LITTLE SECTION
                case (char)0x104B: // MYANMAR SIGN SECTION
                case (char)0x104C: // MYANMAR SYMBOL LOCATIVE
                case (char)0x104D: // MYANMAR SYMBOL COMPLETED
                case (char)0x104E: // MYANMAR SYMBOL AFOREMENTIONED
                case (char)0x104F: // MYANMAR SYMBOL GENITIVE
                case (char)0x10FB: // GEORGIAN PARAGRAPH SEPARATOR
                case (char)0x1361: // ETHIOPIC WORDSPACE
                case (char)0x1362: // ETHIOPIC FULL STOP
                case (char)0x1363: // ETHIOPIC COMMA
                case (char)0x1364: // ETHIOPIC SEMICOLON
                case (char)0x1365: // ETHIOPIC COLON
                case (char)0x1366: // ETHIOPIC PREFACE COLON
                case (char)0x1367: // ETHIOPIC QUESTION MARK
                case (char)0x1368: // ETHIOPIC PARAGRAPH SEPARATOR
                case (char)0x166D: // CANADIAN SYLLABICS CHI SIGN
                case (char)0x166E: // CANADIAN SYLLABICS FULL STOP
                case (char)0x16EB: // RUNIC SINGLE PUNCTUATION
                case (char)0x16EC: // RUNIC MULTIPLE PUNCTUATION
                case (char)0x16ED: // RUNIC CROSS PUNCTUATION
                case (char)0x1735: // PHILIPPINE SINGLE PUNCTUATION
                case (char)0x1736: // PHILIPPINE DOUBLE PUNCTUATION
                case (char)0x17D4: // KHMER SIGN KHAN
                case (char)0x17D5: // KHMER SIGN BARIYOOSAN
                case (char)0x17D6: // KHMER SIGN CAMNUC PII KUUH
                case (char)0x17D8: // KHMER SIGN BEYYAL
                case (char)0x17D9: // KHMER SIGN PHNAEK MUAN
                case (char)0x17DA: // KHMER SIGN KOOMUUT
                case (char)0x1800: // MONGOLIAN BIRGA
                case (char)0x1801: // MONGOLIAN ELLIPSIS
                case (char)0x1802: // MONGOLIAN COMMA
                case (char)0x1803: // MONGOLIAN FULL STOP
                case (char)0x1804: // MONGOLIAN COLON
                case (char)0x1805: // MONGOLIAN FOUR DOTS
                case (char)0x1807: // MONGOLIAN SIBE SYLLABLE BOUNDARY MARKER
                case (char)0x1808: // MONGOLIAN MANCHU COMMA
                case (char)0x1809: // MONGOLIAN MANCHU FULL STOP
                case (char)0x180A: // MONGOLIAN NIRUGU
                case (char)0x1944: // LIMBU EXCLAMATION MARK
                case (char)0x1945: // LIMBU QUESTION MARK
                case (char)0x2016: // DOUBLE VERTICAL LINE
                case (char)0x2017: // DOUBLE LOW LINE
                case (char)0x2020: // DAGGER
                case (char)0x2021: // DOUBLE DAGGER
                case (char)0x2022: // BULLET
                case (char)0x2023: // TRIANGULAR BULLET
                case (char)0x2024: // ONE DOT LEADER
                case (char)0x2025: // TWO DOT LEADER
                case (char)0x2026: // HORIZONTAL ELLIPSIS
                case (char)0x2027: // HYPHENATION POINT
                case (char)0x2030: // PER MILLE SIGN
                case (char)0x2031: // PER TEN THOUSAND SIGN
                case (char)0x2032: // PRIME
                case (char)0x2033: // DOUBLE PRIME
                case (char)0x2034: // TRIPLE PRIME
                case (char)0x2035: // REVERSED PRIME
                case (char)0x2036: // REVERSED DOUBLE PRIME
                case (char)0x2037: // REVERSED TRIPLE PRIME
                case (char)0x2038: // CARET
                case (char)0x203B: // REFERENCE MARK
                case (char)0x203C: // DOUBLE EXCLAMATION MARK
                case (char)0x203D: // INTERROBANG
                case (char)0x203E: // OVERLINE
                case (char)0x2041: // CARET INSERTION POINT
                case (char)0x2042: // ASTERISM
                case (char)0x2043: // HYPHEN BULLET
                case (char)0x2047: // DOUBLE QUESTION MARK
                case (char)0x2048: // QUESTION EXCLAMATION MARK
                case (char)0x2049: // EXCLAMATION QUESTION MARK
                case (char)0x204A: // TIRONIAN SIGN ET
                case (char)0x204B: // REVERSED PILCROW SIGN
                case (char)0x204C: // BLACK LEFTWARDS BULLET
                case (char)0x204D: // BLACK RIGHTWARDS BULLET
                case (char)0x204E: // LOW ASTERISK
                case (char)0x204F: // REVERSED SEMICOLON
                case (char)0x2050: // CLOSE UP
                case (char)0x2051: // TWO ASTERISKS ALIGNED VERTICALLY
                case (char)0x2053: // SWUNG DASH
                case (char)0x2057: // QUADRUPLE PRIME
                case (char)0x23B6: // BOTTOM SQUARE BRACKET OVER TOP SQUARE BRACKET
                case (char)0x3001: // IDEOGRAPHIC COMMA
                case (char)0x3002: // IDEOGRAPHIC FULL STOP
                case (char)0x3003: // DITTO MARK
                case (char)0x303D: // PART ALTERNATION MARK
                case (char)0xFE30: // PRESENTATION FORM FOR VERTICAL TWO DOT LEADER
                case (char)0xFE45: // SESAME DOT
                case (char)0xFE46: // WHITE SESAME DOT
                case (char)0xFE49: // DASHED OVERLINE
                case (char)0xFE4A: // CENTRELINE OVERLINE
                case (char)0xFE4B: // WAVY OVERLINE
                case (char)0xFE4C: // DOUBLE WAVY OVERLINE
                case (char)0xFE50: // SMALL COMMA
                case (char)0xFE51: // SMALL IDEOGRAPHIC COMMA
                case (char)0xFE52: // SMALL FULL STOP
                case (char)0xFE54: // SMALL SEMICOLON
                case (char)0xFE55: // SMALL COLON
                case (char)0xFE56: // SMALL QUESTION MARK
                case (char)0xFE57: // SMALL EXCLAMATION MARK
                case (char)0xFE5F: // SMALL NUMBER SIGN
                case (char)0xFE60: // SMALL AMPERSAND
                case (char)0xFE61: // SMALL ASTERISK
                case (char)0xFE68: // SMALL REVERSE SOLIDUS
                case (char)0xFE6A: // SMALL PERCENT SIGN
                case (char)0xFE6B: // SMALL COMMERCIAL AT
                case (char)0xFF01: // FULLWIDTH EXCLAMATION MARK
                case (char)0xFF02: // FULLWIDTH QUOTATION MARK
                case (char)0xFF03: // FULLWIDTH NUMBER SIGN
                case (char)0xFF05: // FULLWIDTH PERCENT SIGN
                case (char)0xFF06: // FULLWIDTH AMPERSAND
                case (char)0xFF07: // FULLWIDTH APOSTROPHE
                case (char)0xFF0A: // FULLWIDTH ASTERISK
                case (char)0xFF0C: // FULLWIDTH COMMA
                case (char)0xFF0E: // FULLWIDTH FULL STOP
                case (char)0xFF0F: // FULLWIDTH SOLIDUS
                case (char)0xFF1A: // FULLWIDTH COLON
                case (char)0xFF1B: // FULLWIDTH SEMICOLON
                case (char)0xFF1F: // FULLWIDTH QUESTION MARK
                case (char)0xFF20: // FULLWIDTH COMMERCIAL AT
                case (char)0xFF3C: // FULLWIDTH REVERSE SOLIDUS
                case (char)0xFF61: // HALFWIDTH IDEOGRAPHIC FULL STOP
                case (char)0xFF64: // HALFWIDTH IDEOGRAPHIC COMMA
                    return true;
                default:
                    return false;
            }
        }

        public static string trimWhitespaceOrPunct(string inputString)
        {
            int wordBegin = 0;
            int wordSize = inputString.Length;

            // Skip any leading whitespace
            while (IsWhitespace(inputString[wordBegin]) || isPunct(inputString[wordBegin]))
            {
                ++wordBegin;
            }

            // Skip any trailing whitespace
            while (IsWhitespace(inputString[wordSize - 1]) ||
                                 isPunct(inputString[wordSize - 1]))
                --wordSize;

            return inputString[wordBegin..^wordSize];
        }

        public static string trimWhitespace(string inputString)
        {
            int wordBegin = 0;
            int wordSize = inputString.Length;

            // Skip any leading whitespace
            while (IsWhitespace(inputString[wordBegin]))
            {
                ++wordBegin;
            }

            // Skip any trailing whitespace
            while (IsWhitespace(inputString[wordSize - 1]))
                --wordSize;

            return inputString[wordBegin..^wordSize];
        }

        public static void normalizeWhitespace(ref string str)
        {
            StringBuilder sb = new StringBuilder(str);
            for (int x = str.Length; x-- > 1;) // >1 -- Don't test the first char
            {
                if (IsWhitespace(str[x]))
                {
                    int y;
                    for (y = x; IsWhitespace(str[y - 1]); --y) ;

                    if (y != x)
                    {
                        // Remove extra spaces

                        x = y;

                        sb.Remove(y + 1, x - y);
                        sb.Insert(y, ' ');
                    }
                }
            }
            str = sb.ToString();
        }

        public static string escapeWildcardSymbols(string str)
        {


            string escaped = str;
            Regex regex = new Regex("([\\[\\]\\?\\*])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            regex.Replace(escaped, "\\\\1");
            return escaped;
        }

        public static String unescapeWildcardSymbols(string str)
        {

            string unescaped = str;
            Regex regex = new Regex("([\\[\\]\\?\\*])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            regex.Replace(unescaped, "[$1]");

            return unescaped;
        }

    }
}

