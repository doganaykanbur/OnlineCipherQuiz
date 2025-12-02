using CipherQuiz.Shared;
using System.Text;

namespace CipherQuiz.Server.Services
{
    public interface IQuestionEngine
    {
        QuestionState Generate(string topic, string difficulty, int totalQuestions, int index, QuizConfig config);
        List<QuestionState> BuildSet(QuizConfig config);
    }

    public class QuestionEngine : IQuestionEngine
    {
        private Random _random = new();

        public List<QuestionState> BuildSet(QuizConfig config)
        {
            var questions = new List<QuestionState>();
            int total = config.QuestionsPerTopicMap.Values.Sum();
            int currentIndex = 0;

            foreach (var kvp in config.QuestionsPerTopicMap)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    var q = Generate(kvp.Key, config.Difficulty, total, currentIndex, config);
                    q.RemainingScore = 100.0 / total; // Basic distribution
                    q.Total = total;
                    questions.Add(q);
                    currentIndex++;
                }
            }

            // Shuffle
            return questions.OrderBy(x => _random.Next()).Select((q, i) => 
            { 
                q.Position = i + 1; 
                return q; 
            }).ToList();
        }

        public QuestionState Generate(string topic, string difficulty, int totalQuestions, int index, QuizConfig config)
        {
            _random = new Random(Guid.NewGuid().GetHashCode());
            return topic.ToLower() switch
            {
                "caesar" => GenerateCaesar(),
                "vigenere" => GenerateVigenere(),
                "base64" => GenerateBase64(),
                "xor" => GenerateXor(),
                "hill" => GenerateHill(),
                "monoalphabetic" => GenerateMonoalphabetic(),
                "playfair" => GeneratePlayfair(config),
                "transposition" => GenerateTransposition(),
                _ => GenerateGeneric(topic)
            };
        }

        private QuestionState GenerateCaesar()
        {
            int shift = _random.Next(1, 26);
            bool encode = _random.Next(2) == 0;
            string plain = GetRandomWord();
            string cipher = CaesarCipher(plain, shift);

            return new QuestionState
            {
                Topic = "Caesar",
                Prompt = encode
                    ? $"\"{plain}\" kelimesini {shift} kaydırarak şifreleyin."
                    : $"Aşağıdaki metin Caesar şifrelemesi ile şifrelenmiştir. Şifreyi çözün.",
                InputHint = encode ? "Şifreli metni girin" : "Düz metni girin (Büyük harf)",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string> { { "Kaydırma", $"+{shift}" } }
                    : new Dictionary<string, string> { { "Şifreli Metin", cipher }, { "Kaydırma", $"+{shift}" } },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateVigenere()
        {
            string key = GetRandomWord(3);
            string plain = GetRandomPhrase(2, 3);
            string cipher = VigenereCipher(plain, key);
            bool encode = _random.Next(2) == 0;

            return new QuestionState
            {
                Topic = "Vigenere",
                Prompt = encode
                    ? $"Anahtar '{key}' ile \"{plain}\" kelimesini şifreleyin."
                    : $"Anahtar '{key}' kullanılarak şifrelenmiş metni çözün.",
                InputHint = encode ? "Şifreli metni yazın" : "Düz metni girin",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string> { { "Anahtar", key } }
                    : new Dictionary<string, string> { { "Anahtar", key }, { "Şifreli Metin", cipher } },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateBase64()
        {
            string plain = GetRandomPhrase(1, 3);
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
            bool encode = _random.Next(2) == 0;

            return new QuestionState
            {
                Topic = "Base64",
                Prompt = encode ? $"\"{plain}\" kelimesini Base64 olarak kodlayın." : "Aşağıdaki Base64 kodlanmış metni çözün.",
                InputHint = encode ? "Encoded çıktıyı yazın" : "Düz metin",
                InputType = "text",
                Data = encode ? new Dictionary<string, string>() : new Dictionary<string, string> { { "Encoded", encoded } },
                CorrectAnswer = encode ? encoded : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateXor()
        {
            int val1 = _random.Next(0, 255);
            int val2 = _random.Next(0, 255);
            int result = val1 ^ val2;
            string Format(int v, string f) => f switch
            {
                "hex" => $"0x{v:X}",
                "bin" => Convert.ToString(v, 2).PadLeft(8, '0'),
                _ => v.ToString()
            };
            var fmt = new[] { "dec", "hex", "bin" }[_random.Next(3)];
            var fmt2 = new[] { "dec", "hex", "bin" }[_random.Next(3)];

            return new QuestionState
            {
                Topic = "Xor",
                Prompt = $"{Format(val1, fmt)} XOR {Format(val2, fmt2)} işleminin sonucu nedir? (Decimal)",
                InputHint = "Sayı girin",
                InputType = "number",
                Data = new Dictionary<string, string> { { "Val1", Format(val1, fmt) }, { "Val2", Format(val2, fmt2) } },
                CorrectAnswer = result.ToString(),
                Attempts = 0
            };
        }

        private QuestionState GenerateHill()
        {
            // Generate invertible 2x2 matrix
            int a, b, c, d;
            do
            {
                a = _random.Next(0, 26);
                b = _random.Next(0, 26);
                c = _random.Next(0, 26);
                d = _random.Next(0, 26);
            } while (!HillCipher.IsInvertible(a, b, c, d));

            var hill = new HillCipher(a, b, c, d);
            string plain = GetRandomWord(4); // Must be even length
            string cipher = hill.Encode(plain);
            bool encode = _random.Next(2) == 0;

            return new QuestionState
            {
                Topic = "Hill",
                Prompt = encode
                    ? "Anahtar matrisi ile verilen düz metni şifreleyin."
                    : "Hill şifrelemesi ile şifrelenmiş metni çözün. Anahtar matrisi verilmiştir.",
                InputHint = encode ? "Şifreli metni yazın" : "Düz metin",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string>
                    {
                        { "Matrix_00", a.ToString() }, { "Matrix_01", b.ToString() },
                        { "Matrix_10", c.ToString() }, { "Matrix_11", d.ToString() },
                        { "Düz Metin", plain }
                    }
                    : new Dictionary<string, string> 
                    { 
                        { "Matrix_00", a.ToString() }, { "Matrix_01", b.ToString() },
                        { "Matrix_10", c.ToString() }, { "Matrix_11", d.ToString() },
                        { "Şifreli Metin", cipher } 
                    },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateMonoalphabetic()
        {
            // Always use a random mixed alphabet, keyword is optional but we'll generate one for display if needed
            // But for the game, we usually show the full mapping or just the key.
            // Let's follow PlayfairGame approach: Show the Mixed Alphabet.
            
            string key = GetRandomWord(5);
            var mono = new MonoalphabeticCipher(key);
            string plain = GetRandomWord();
            string cipher = mono.Encode(plain);
            bool encode = _random.Next(2) == 0;

            return new QuestionState
            {
                Topic = "Monoalphabetic",
                Prompt = encode
                    ? $"Aşağıdaki alfabe eşleşmesini kullanarak \"{plain}\" metnini şifreleyin."
                    : "Aşağıdaki alfabe eşleşmesini kullanarak şifrelenmiş metni çözün.",
                InputHint = encode ? "Şifreli metin" : "Düz metin",
                InputType = "text",
                Data = encode 
                    ? new Dictionary<string, string> 
                    { 
                        { "MixedAlphabet", mono.MixedAlphabet },
                        { "Düz Metin", plain } 
                    }
                    : new Dictionary<string, string> 
                    { 
                        { "MixedAlphabet", mono.MixedAlphabet },
                        { "Şifreli Metin", cipher } 
                    },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GeneratePlayfair(QuizConfig config)
        {
            string key = GetRandomWord(5);
            var playfair = new PlayfairCipher(key);
            string plain = GetRandomWord(6); // Even length preferred
            string cipher = playfair.Encode(plain);
            bool encode = _random.Next(2) == 0;
            string decoded = playfair.Decode(cipher);
            string matrix = playfair.MatrixToString();

            return new QuestionState
            {
                Topic = "Playfair",
                Prompt = encode
                    ? $"Anahtar '{key}' ile \"{plain}\" ifadesini Playfair ile şifreleyin."
                    : "Playfair şifrelemesi ile şifrelenmiş metni çözün. Anahtar kelime verilmiştir.",
                InputHint = encode ? "Şifreli metni yazın" : "Düz metin (X'leri dahil edin)",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string> 
                    { 
                        { "Anahtar Kelime", key },
                        { "Matris", matrix },
                        { "Düz Metin", plain } 
                    }
                    : new Dictionary<string, string> 
                    { 
                        { "Anahtar Kelime", key },
                        { "Matris", matrix },
                        { "Şifreli Metin", cipher } 
                    },
                CorrectAnswer = encode ? cipher : decoded,
                Attempts = 0
            };
        }

        private QuestionState GenerateTransposition()
        {
            string key = GetRandomWord(5);
            var trans = new TranspositionCipher(key);
            string plain = GetRandomWord(10);
            string cipher = trans.Encode(plain);
            bool encode = _random.Next(2) == 0;

            return new QuestionState
            {
                Topic = "Transposition",
                Prompt = encode ? $"Anahtar '{key}' ile metni şifreleyin." : "Transposition (Sütun) şifrelemesi ile şifrelenmiş metni çözün.",
                InputHint = encode ? "Şifreli metni yazın" : "Düz metin",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string> 
                    { 
                        { "Anahtar Kelime", key },
                        { "Düz Metin", plain } 
                    }
                    : new Dictionary<string, string> 
                    { 
                        { "Anahtar Kelime", key },
                        { "Şifreli Metin", cipher } 
                    },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateGeneric(string topic)
        {
            string plain = GetRandomWord();
            return new QuestionState
            {
                Topic = topic,
                Prompt = $"{topic} şifrelemesi ile şifrelenmiş metni çözün (Simülasyon).",
                InputHint = "Cevap: " + plain,
                InputType = "text",
                Data = new Dictionary<string, string> { { "Şifreli", new string(plain.Reverse().ToArray()) } },
                CorrectAnswer = plain,
                Attempts = 0
            };
        }

        // Helpers
        private string GetRandomWord(int length = 5)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var stringChars = new char[length];
            for (int i = 0; i < length; i++) stringChars[i] = chars[_random.Next(chars.Length)];
            return new string(stringChars);
        }

        private string GetRandomPhrase(int minWords = 1, int maxWords = 3)
        {
            var parts = new List<string>();
            int count = _random.Next(minWords, maxWords + 1);
            for (int i = 0; i < count; i++) parts.Add(GetRandomWord(_random.Next(3, 8)));
            return string.Join(" ", parts);
        }

        private string CaesarCipher(string input, int shift)
        {
            char Cipher(char ch)
            {
                if (!char.IsLetter(ch)) return ch;
                char d = char.IsUpper(ch) ? 'A' : 'a';
                return (char)((((ch + shift) - d) % 26) + d);
            }
            return string.Concat(input.Select(Cipher));
        }

        private string VigenereCipher(string input, string key)
        {
            string output = "";
            int keyIndex = 0;
            foreach (char c in input)
            {
                if (char.IsLetter(c))
                {
                    char d = char.IsUpper(c) ? 'A' : 'a';
                    int shift = char.ToUpper(key[keyIndex % key.Length]) - 'A';
                    output += (char)((((c + shift) - d) % 26) + d);
                    keyIndex++;
                }
                else
                {
                    output += c;
                }
            }
            return output;
        }
    }
}
