using System;

namespace TaskCanvas
{
    public static class FractionalIndex
    {
        private const string ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        public static string First() => "a";

        public static string Between(string before, string after)
        {
            if (string.IsNullOrEmpty(after))
            {
                return string.IsNullOrEmpty(before) ? "a" : before + "V";
            }

            if (string.IsNullOrEmpty(before))
            {
                int midIndex = ALPHABET.IndexOf(after[0]) / 2;
                if (midIndex > 0) return ALPHABET[midIndex].ToString();
                return "0" + after;
            }

            return FindMidpoint(before, after);
        }

        private static string FindMidpoint(string a, string b)
        {
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i])
                i++;

            if (i < a.Length)
            {
                int aChar = ALPHABET.IndexOf(a[i]);
                int bChar = i < b.Length ? ALPHABET.IndexOf(b[i]) : ALPHABET.Length;

                if (aChar >= 0 && bChar > aChar + 1)
                {
                    int mid = (aChar + bChar) / 2;
                    return a.Substring(0, i) + ALPHABET[mid];
                }
            }

            if (i == a.Length && i < b.Length)
            {
                int bChar = ALPHABET.IndexOf(b[i]);
                if (bChar > 0 || i < b.Length - 1)
                {
                    int mid = bChar / 2;
                    return a + ALPHABET[mid];
                }
            }

            return a + "V";
        }

        public static string After(string key)
        {
            return string.IsNullOrEmpty(key) ? "a" : key + "V";
        }

        public static string Before(string key)
        {
            if (string.IsNullOrEmpty(key)) return "a";

            int idx = ALPHABET.IndexOf(key[0]);
            if (idx > 1)
            {
                return ALPHABET[idx / 2].ToString();
            }
            return "0" + key;
        }
    }
}
