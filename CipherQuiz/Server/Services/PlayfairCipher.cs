using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CipherQuiz.Server.Services
{
    public class PlayfairCipher
    {
        // 5x5 Playfair matrix (I/J share a cell)
        private char[,] matrix = new char[5,5];
        // Flattened 25‑character key used to fill the matrix row‑wise
        private string key;
        private const string ALPHABET = "ABCDEFGHIKLMNOPQRSTUVWXYZ"; // J merged with I

        public PlayfairCipher(string key)
        {
            // Prepare and deduplicate the keyword, then build the 5x5 matrix
            this.key = PrepareKey(key);
            BuildMatrix();
        }

        private string PrepareKey(string k)
        {
            if (string.IsNullOrWhiteSpace(k)) k = "KEYWORD";
            k = Regex.Replace(k.ToUpper(), "[^A-Z]", "");
            k = k.Replace("J", "I"); // J'yi I ile değiştir
            var seen = new HashSet<char>();
            var sb = new StringBuilder();
            foreach (var ch in k)
            {
                if (!seen.Contains(ch))
                {
                    seen.Add(ch);
                    sb.Append(ch);
                }
            }
            foreach (var ch in ALPHABET)
            {
                if (!seen.Contains(ch))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private void BuildMatrix()
        {
            // Copy the 25‑char flattened key into the matrix row‑wise
            for (int i = 0; i < 25; i++)
            {
                matrix[i / 5, i % 5] = key[i];
            }
        }

        private (int, int) FindPos(char c)
        {
            // Treat J as I while searching in the matrix
            if (c == 'J') c = 'I';
            for (int r = 0; r < 5; r++)
                for (int c2 = 0; c2 < 5; c2++)
                    if (matrix[r, c2] == c) return (r, c2);
            throw new Exception("Char not in matrix");
        }

        private List<(char,char)> PrepareDigraphs(string input)
        {
            // Uppercase, keep A..Z, merge J→I, then split into pairs
            input = Regex.Replace(input.ToUpper(), "[^A-Z]", "");
            input = input.Replace("J", "I");
            var result = new List<(char,char)>();
            int i = 0;
            while (i < input.Length)
            {
                char a = input[i];
                char b = (i+1 < input.Length) ? input[i+1] : 'X';
                if (a == b)
                {
                    // Duplicate letters: insert an 'X' between them
                    result.Add((a, 'X'));
                    i += 1;
                }
                else
                {
                    result.Add((a, b));
                    i += 2;
                }
            }
            // Even count guaranteed by the loop/padding above
            return result;
        }

        public string Encode(string plaintext)
        {
            var pairs = PrepareDigraphs(plaintext);
            var sb = new StringBuilder();
            foreach (var (a,b) in pairs)
            {
                var pa = FindPos(a);
                var pb = FindPos(b);
                if (pa.Item1 == pb.Item1) // same row → shift right
                {
                    sb.Append(matrix[pa.Item1, (pa.Item2 + 1) % 5]);
                    sb.Append(matrix[pb.Item1, (pb.Item2 + 1) % 5]);
                }
                else if (pa.Item2 == pb.Item2) // same column → shift down
                {
                    sb.Append(matrix[(pa.Item1 + 1) % 5, pa.Item2]);
                    sb.Append(matrix[(pb.Item1 + 1) % 5, pb.Item2]);
                }
                else // rectangle → swap columns
                {
                    sb.Append(matrix[pa.Item1, pb.Item2]);
                    sb.Append(matrix[pb.Item1, pa.Item2]);
                }
            }
            return sb.ToString();
        }

        public string Decode(string ciphertext)
        {
            var cleaned = Regex.Replace(ciphertext.ToUpper(), "[^A-Z]", "");
            var pairs = new List<(char,char)>();
            for (int i = 0; i < cleaned.Length; i += 2)
            {
                char a = cleaned[i];
                char b = (i+1 < cleaned.Length) ? cleaned[i+1] : 'X';
                pairs.Add((a,b));
            }

            var sb = new StringBuilder();
            foreach (var (a,b) in pairs)
            {
                var pa = FindPos(a);
                var pb = FindPos(b);
                if (pa.Item1 == pb.Item1) // same row → shift left
                {
                    sb.Append(matrix[pa.Item1, (pa.Item2 + 4) % 5]);
                    sb.Append(matrix[pb.Item1, (pb.Item2 + 4) % 5]);
                }
                else if (pa.Item2 == pb.Item2) // same column → shift up
                {
                    sb.Append(matrix[(pa.Item1 + 4) % 5, pa.Item2]);
                    sb.Append(matrix[(pb.Item1 + 4) % 5, pb.Item2]);
                }
                else // rectangle → swap columns
                {
                    sb.Append(matrix[pa.Item1, pb.Item2]);
                    sb.Append(matrix[pb.Item1, pa.Item2]);
                }
            }
            return sb.ToString();
        }

        // Optional: return matrix as string for UI
        public string MatrixToString()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                    sb.Append(matrix[r,c]);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
