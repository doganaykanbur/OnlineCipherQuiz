using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CipherQuiz.Server.Services
{
    public class HillCipher
    {
        // 2x2 Hill cipher with A=0..Z=25
        private readonly int[,] key;        // 2x2
        private readonly int[,] invKey;     // 2x2 inverse mod 26

        public HillCipher(int a, int b, int c, int d)
        {
            // Normalize elements modulo 26 and pre-compute inverse matrix
            key = new int[2,2] { { Mod(a), Mod(b) }, { Mod(c), Mod(d) } };
            invKey = Invert2x2(key);
        }

        public int[,] GetKey() => (int[,]) key.Clone();
        public int[,] GetInverseKey() => (int[,]) invKey.Clone();

        public static int Mod(int x) => ((x % 26) + 26) % 26;

        public static bool IsInvertible(int a, int b, int c, int d)
        {
            // A 2x2 matrix is invertible mod 26 iff gcd(det, 26) == 1
            int det = Mod(a * d - b * c);
            return Gcd(det, 26) == 1;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) { int t = a % b; a = b; b = t; }
            return Math.Abs(a);
        }

        private static int ModInverse(int x)
        {
            x = Mod(x);
            for (int i = 1; i < 26; i++) if ((x * i) % 26 == 1) return i; // brute-force since 26 is small
            throw new ArgumentException("No modular inverse for given value under mod 26");
        }

        private static int[,] Invert2x2(int[,] m)
        {
            int a = m[0,0], b = m[0,1], c = m[1,0], d = m[1,1];
            int det = Mod(a * d - b * c);
            if (Gcd(det, 26) != 1) throw new ArgumentException("Matrix not invertible mod 26");
            int invDet = ModInverse(det);
            // adjugate = [[d, -b], [-c, a]]
            int[,] adj = new int[2,2] { { d, Mod(-b) }, { Mod(-c), a } };
            int[,] inv = new int[2,2];
            for (int r = 0; r < 2; r++)
                for (int c2 = 0; c2 < 2; c2++)
                    inv[r, c2] = Mod(adj[r, c2] * invDet);
            return inv;
        }

        private static IEnumerable<(int,int)> ToPairs(string s)
        {
            // Map A..Z to 0..25, discard non-letters, pad odd length with X
            var nums = new List<int>();
            foreach (char ch in s.ToUpper())
            {
                if (ch >= 'A' && ch <= 'Z') nums.Add(ch - 'A');
            }
            if (nums.Count % 2 == 1) nums.Add('X' - 'A');
            for (int i = 0; i < nums.Count; i += 2)
                yield return (nums[i], nums[i+1]);
        }

        private static string FromNumbers(List<int> nums)
        {
            var sb = new StringBuilder(nums.Count);
            foreach (var n in nums) sb.Append((char)('A' + Mod(n)));
            return sb.ToString();
        }

        private static List<int> MultiplyPairs(IEnumerable<(int,int)> pairs, int[,] m)
        {
            var outNums = new List<int>();
            foreach (var (x, y) in pairs)
            {
                int nx = Mod(m[0,0] * x + m[0,1] * y); // first row
                int ny = Mod(m[1,0] * x + m[1,1] * y); // second row
                outNums.Add(nx);
                outNums.Add(ny);
            }
            return outNums;
        }

        public string Encode(string plaintext)
        {
            var pairs = ToPairs(plaintext);
            var nums = MultiplyPairs(pairs, key);
            return FromNumbers(nums);
        }

        public string Decode(string ciphertext)
        {
            var pairs = ToPairs(ciphertext);
            var nums = MultiplyPairs(pairs, invKey);
            return FromNumbers(nums);
        }
    }
}
