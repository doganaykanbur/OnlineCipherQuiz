using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace CipherQuiz.Server.Services
{
    public class MonoalphabeticCipher
    {
        // map: plaintext A..Z -> mixed alphabet
        private readonly char[] map = new char[26];
        // inv: ciphertext mixed -> A..Z
        private readonly char[] inv = new char[26];
        // Final mixed alphabet (length 26)
        private readonly string mixed;

        public MonoalphabeticCipher(string? keyword)
        {
            // If keyword is null/empty, generate a random permutation of A..Z.
            if (string.IsNullOrWhiteSpace(keyword))
            {
                mixed = BuildRandomMixed();
            }
            else
            {
                // Build mixed alphabet from keyword + remaining letters (A..Z), uppercase only
                var key = new string(keyword.ToUpper().Where(ch => ch >= 'A' && ch <= 'Z').ToArray());
                if (key.Length == 0)
                {
                    mixed = BuildRandomMixed();
                }
                else
                {
                    var seen = new HashSet<char>();
                    var sb = new StringBuilder(26);
                    foreach (var ch in key)
                        if (seen.Add(ch)) sb.Append(ch);
                    for (char ch = 'A'; ch <= 'Z'; ch++)
                        if (seen.Add(ch)) sb.Append(ch);
                    mixed = sb.ToString(); // length 26
                }
            }
            for (int i = 0; i < 26; i++)
            {
                map[i] = mixed[i]; // A+i -> mixed[i]
                inv[mixed[i]-'A'] = (char)('A' + i); // reverse map for decode
            }
        }

        public string MixedAlphabet => mixed;
        public string PlainAlphabet => "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private static string BuildRandomMixed()
        {
            // Fisher–Yates shuffle over A..Z
            var arr = Enumerable.Range(0, 26).Select(i => (char)('A' + i)).ToArray();
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return new string(arr);
        }

        public string Encode(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return string.Empty;
            var outSb = new StringBuilder(plaintext.Length);
            foreach (var raw in plaintext.ToUpper())
            {
                if (raw >= 'A' && raw <= 'Z') outSb.Append(map[raw - 'A']);
                else outSb.Append(raw);
            }
            return outSb.ToString();
        }

        public string Decode(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
            var outSb = new StringBuilder(ciphertext.Length);
            foreach (var raw in ciphertext.ToUpper())
            {
                if (raw >= 'A' && raw <= 'Z') outSb.Append(inv[raw - 'A']);
                else outSb.Append(raw);
            }
            return outSb.ToString();
        }
    }
}
