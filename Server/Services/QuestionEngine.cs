using CipherQuiz.Shared;
using System.Text;

namespace CipherQuiz.Server.Services
{
    public interface IQuestionEngine
    {
        QuestionState Generate(string topic, int difficulty, int totalQuestions, int index, QuizConfig config, string language);
        QuestionState GenerateFromCustom(CustomQuestion cq, string language);
        Task<List<QuestionState>> BuildSet(QuizConfig config, string language);
    }

    public class QuestionEngine : IQuestionEngine
    {
        private Random _random = new();
        private readonly CustomQuestionStore _customStore;

        public QuestionEngine(CustomQuestionStore customStore)
        {
            _customStore = customStore;
        }

        public async Task<List<QuestionState>> BuildSet(QuizConfig config, string language)
        {
            var questions = new List<QuestionState>();
            int total = config.QuestionsPerTopicMap.Values.Sum() + config.CustomQuestionIds.Count;
            int currentIndex = 0;

            // 1. Generate Standard Questions
            foreach (var kvp in config.QuestionsPerTopicMap)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    var q = Generate(kvp.Key, config.Difficulty, total, currentIndex, config, language);
                    q.RemainingScore = 100.0 / total;
                    q.Total = total;
                    questions.Add(q);
                    currentIndex++;
                }
            }

            // 2. Generate Custom Questions
            if (config.CustomQuestionIds.Any())
            {
                var allCustom = await _customStore.GetQuestionsAsync();
                var selectedCustom = allCustom.Where(x => config.CustomQuestionIds.Contains(x.Id)).ToList();
                
                foreach (var cq in selectedCustom)
                {
                    var q = GenerateFromCustom(cq, language);
                    q.RemainingScore = 100.0 / total;
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

        public QuestionState Generate(string topic, int difficulty, int totalQuestions, int index, QuizConfig config, string language)
        {
            _random = new Random(Guid.NewGuid().GetHashCode());
            return topic.ToLower() switch
            {
                "caesar" => GenerateCaesar(config, language),
                "vigenere" => GenerateVigenere(config, language),
                "base64" => GenerateBase64(language), // No key, so standard is fine or maybe add meaningful?
                "xor" => GenerateXor(config, language),
                "hill" => GenerateHill(language), // TODO: logic for Hill crypto
                "monoalphabetic" => GenerateMonoalphabetic(language),
                "playfair" => GeneratePlayfair(config, language),
                "transposition" => GenerateTransposition(config, language),
                _ => GenerateGeneric(topic, language)
            };
        }

        private QuestionState GenerateCaesar(QuizConfig config, string lang)
        {
            int shift = _random.Next(1, 26);
            bool isCrypto = config.IsCryptanalysis;
            
            // In Crypto mode: Always decrypt, Text is Meaningful, Key is HIDDEN.
            string plain = isCrypto ? GetMeaningfulText(lang) : GetRandomWord();
            string cipher = CaesarCipher(plain, shift);
            bool encode = !isCrypto && _random.Next(2) == 0;

            var promptTr = isCrypto
                ? "Aşağıdaki metin Sezar (Caesar) şifreleme yöntemiyle şifrelenmiştir. Harf frekans analizi yaparak şifreyi kırınız ve anlamlı düz metni bulunuz."
                : (encode 
                    ? $"Aşağıdaki \"{plain}\" metnini, {shift} birim öteleme kullanarak Sezar şifreleyiniz."
                    : $"Aşağıdaki metin Sezar yöntemiyle ({shift} birim öteleme) şifrelenmiştir. Şifreyi çözünüz.");
            
            var promptEn = isCrypto
                ? "The text below is encrypted using Caesar cipher. Perform frequency analysis to crack the code and find the meaningful plaintext."
                : (encode
                    ? $"Encrypt the text \"{plain}\" using Caesar cipher with a shift of {shift}."
                    : $"The text below is encrypted using Caesar cipher with a shift of {shift}. Decrypt it.");

            var hintTr = isCrypto ? "Şifre (Shift) ve Düz Metin" : (encode ? "Şifreli metni girin" : "Düz metni girin (Büyük harf)");
            var hintEn = isCrypto ? "Shift and Plaintext" : (encode ? "Enter encrypted text" : "Enter plaintext (Uppercase)");

            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
            var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";
            var lblShift = lang == "en" ? "Shift" : "Kaydırma";

            var data = new Dictionary<string, string>();
            if (isCrypto)
            {
                data.Add(lblCipher, cipher);
                data.Add(lblIndices, GetAlphabetIndices());
                // Shift hidden
            }
            else
            {
                data.Add(lblShift, $"+{shift}"); // Show Key
                data.Add(lblIndices, GetAlphabetIndices());
                if (encode) ; 
                else data.Add(lblCipher, cipher);
            }

            return new QuestionState
            {
                Topic = "Caesar",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = lang == "en" ? hintEn : hintTr,
                InputType = isCrypto ? "caesar_analysis" : "text",
                Data = data,
                CorrectAnswer = isCrypto ? $"{shift}|{plain}" : (encode ? cipher : plain),
                Attempts = 0
            };
        }

        private QuestionState GenerateVigenere(QuizConfig config, string lang)
        {
            bool isCrypto = config.IsCryptanalysis;
            string key = GetRandomWord(3);
            string plain = isCrypto ? GetMeaningfulText(lang) : GetRandomPhrase(2, 3);
            string cipher = VigenereCipher(plain, key);
            bool encode = !isCrypto && _random.Next(2) == 0;

            var promptTr = isCrypto
                ? $"Aşağıda düz metin ve şifreli hali verilmiştir. Vigenere şifrelemesinde kullanılan **Anahtar Kelimeyi (Key)** bulunuz."
                : (encode
                    ? $"\"{plain}\" metnini, '{key}' anahtar kelimesini kullanarak Vigenere şifreleme yöntemiyle şifreleyiniz."
                    : $"Aşağıdaki metin '{key}' anahtarı kullanılarak Vigenere yöntemiyle şifrelenmiştir. Şifreyi çözünüz.");
            
            var promptEn = isCrypto
                ? "The plaintext and ciphertext are given below. Find the Vigenere **Keyword** used."
                : (encode
                    ? $"Encrypt \"{plain}\" using Vigenere cipher with key '{key}'."
                    : $"The text below is encrypted using Vigenere cipher with key '{key}'. Decrypt it.");

            var hintTr = isCrypto ? "Anahtar kelimeyi girin" : (encode ? "Şifreli metni yazın" : "Düz metni girin");
            var hintEn = isCrypto ? "Enter keyword" : (encode ? "Enter encrypted text" : "Enter plaintext");

            var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
            var lblKey = lang == "en" ? "Key" : "Anahtar";
            var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";

            var data = new Dictionary<string, string>();
            data.Add(lblIndices, GetAlphabetIndices());

            if (isCrypto)
            {
                data.Add(lblPlain, plain);
                data.Add(lblCipher, cipher);
            }
            else
            {
                data.Add(lblKey, key);
                if (!encode) data.Add(lblCipher, cipher);
            }

            return new QuestionState
            {
                Topic = "Vigenere",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = lang == "en" ? hintEn : hintTr,
                InputType = "text",
                Data = data,
                CorrectAnswer = isCrypto ? key : (encode ? cipher : plain),
                Attempts = 0
            };
        }
        private QuestionState GenerateBase64(string lang)
        {
            string plain = GetRandomPhrase(1, 3);
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
            bool encode = _random.Next(2) == 0;

            var promptTr = encode ? $"\"{plain}\" metnini Base64 formatına kodlayınız." : "Aşağıda verilen Base64 kodlu metnin orijinal halini bulunuz.";
            var promptEn = encode ? $"Encode \"{plain}\" to Base64." : "Decode the following Base64 text.";
            var hintTr = encode ? "Encoded çıktıyı yazın" : "Düz metin";
            var hintEn = encode ? "Enter encoded output" : "Enter plaintext";

            return new QuestionState
            {
                Topic = "Base64",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = lang == "en" ? hintEn : hintTr,
                InputType = "text",
                Data = encode ? new Dictionary<string, string>() : new Dictionary<string, string> { { "Encoded", encoded } },
                CorrectAnswer = encode ? encoded : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateXor(QuizConfig config, string lang)
        {
            // For Xor, finding key (Val2) if Val1 and Result is given?
            // "Cryptanalysis" on XOR usually means finding the key.
            // Val1 XOR Val2 = Result.
            // If we give Val1 and Result, finding Val2 is trivial (Result ^ Val1).
            // But let's support it.
            
            bool isCrypto = config.IsCryptanalysis;
            int val1 = _random.Next(0, 255);
            int val2 = _random.Next(0, 255);
            int result = val1 ^ val2;

            var lblVal1 = lang == "en" ? "Value 1" : "Değer 1";
            var lblVal2 = lang == "en" ? "Value 2" : "Değer 2";
            var lblResult = lang == "en" ? "Result" : "Sonuç";

            if (isCrypto)
            {
                 // Given Val1 and Result, find Val2 (Key).
                 var promptTr = $"XOR İşlemi: {val1} XOR [Anahtar] = {result}. Anahtar (Key) değerini bulunuz.";
                 var promptEn = $"XOR Operation: {val1} XOR [Key] = {result}. Find the Key value.";
                 
                 return new QuestionState
                 {
                    Topic = "Xor",
                    Prompt = lang == "en" ? promptEn : promptTr,
                    InputHint = "Sayı",
                    InputType = "number",
                    Data = new Dictionary<string, string> { { lblVal1, val1.ToString() }, { lblResult, result.ToString() } },
                    CorrectAnswer = val2.ToString(),
                    Attempts = 0
                 };
            }
            
            string Format(int v, string f) => f switch
            {
                "hex" => $"0x{v:X}",
                "bin" => Convert.ToString(v, 2).PadLeft(8, '0'),
                _ => v.ToString()
            };
            var fmt = new[] { "dec", "hex", "bin" }[_random.Next(3)];
            var fmt2 = new[] { "dec", "hex", "bin" }[_random.Next(3)];

            var promptTr2 = $"Aşağıdaki {Format(val1, fmt)} ve {Format(val2, fmt2)} değerlerinin XOR işleminin sonucunu onluk (decimal) tabanda yazınız.";
            var promptEn2 = $"Calculate XOR of {Format(val1, fmt)} and {Format(val2, fmt2)} and write result in decimal.";

            return new QuestionState
            {
                Topic = "Xor",
                Prompt = lang == "en" ? promptEn2 : promptTr2,
                InputHint = lang == "en" ? "Enter number" : "Sayı girin",
                InputType = "number",
                Data = new Dictionary<string, string> { { "Val1", Format(val1, fmt) }, { "Val2", Format(val2, fmt2) } },
                CorrectAnswer = result.ToString(),
                Attempts = 0
            };
        }

        private QuestionState GenerateHill(string lang)
        {
            // Keeping Hill standard for now as Cracking Hill is too hard without tools.
            return GenerateHillStandard(lang); 
        }
        
        // Renamed original GenerateHill to prevent signature conflict if I needed to change it, 
        // but actually I can just keep it or update signature. 
        // Let's just update signature to match the call in Generate.
        // Wait, Generate calls `GenerateHill(language)`. So signature is `string lang`. 
        // I did NOT update GenerateHill call in Generate. So it matches.
        // But I need to define GenerateHillStandard or just use the body.
        // I will restore the body of GenerateHill below.
        
        private QuestionState GenerateHillStandard(string lang)
        {
             // ... Logic from previous implementation ...
             // To save tokens/complexity, I'll just copy the previous body here or use a helper.
             // Actually I'll just leave GenerateHill logic as is in the next block if I didn't change it.
             // But I am replacing a big block. I need to be careful.
             // The previous block I'm replacing starts at GenerateBase64 end (line 166 approx) 
             // and goes to VigenereCipher end (line 436).
             // NO. The ReplaceFileContent Target StartLine is 168 (GenerateXor start) to 436.
             // I need to provide implementations for Xor, Hill, Mono, Playfair, Transposition helpers.
             
             // RE-IMPLEMENTING HILL (Standard)
            int a, b, c, d;
            do
            {
                a = _random.Next(0, 26);
                b = _random.Next(0, 26);
                c = _random.Next(0, 26);
                d = _random.Next(0, 26);
            } while (!HillCipher.IsInvertible(a, b, c, d));

            var hill = new HillCipher(a, b, c, d);
            string plain = GetRandomWord(4);
            string cipher = hill.Encode(plain);
            bool encode = _random.Next(2) == 0;

            var promptTr = encode
                ? "Aşağıda verilen anahtar matrisini kullanarak düz metni Hill şifreleme yöntemiyle şifreleyiniz."
                : "Aşağıdaki metin Hill yöntemiyle şifrelenmiştir. Verilen anahtar matrisini kullanarak şifreyi çözünüz.";
            var promptEn = encode
                ? "Encrypt the plaintext using Hill cipher with the given key matrix."
                : "Decrypt the ciphertext using Hill cipher with the given key matrix.";

            var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";
            var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";

            return new QuestionState
            {
                Topic = "Hill",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = "Cevap",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string> { { "Matrix_00", a.ToString() }, { "Matrix_01", b.ToString() }, { "Matrix_10", c.ToString() }, { "Matrix_11", d.ToString() }, { lblPlain, plain }, { lblIndices, GetAlphabetIndices() } }
                    : new Dictionary<string, string> { { "Matrix_00", a.ToString() }, { "Matrix_01", b.ToString() }, { "Matrix_10", c.ToString() }, { "Matrix_11", d.ToString() }, { lblCipher, cipher }, { lblIndices, GetAlphabetIndices() } },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateMonoalphabetic(string lang)
        {
            // Standard
            string key = GetRandomWord(5);
            var mono = new MonoalphabeticCipher(key);
            string plain = GetRandomWord();
            string cipher = mono.Encode(plain);
            bool encode = _random.Next(2) == 0;
            
             var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
             var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
            
              return new QuestionState
            {
                Topic = "Monoalphabetic",
                Prompt = lang == "en" ? (encode ? "Encrypt" : "Decrypt") : (encode ? "Şifreleyin" : "Şifreyi Çözün"),
                InputHint = "Cevap",
                InputType = "text",
                Data = new Dictionary<string, string> { { "MixedAlphabet", mono.MixedAlphabet }, { (encode ? lblPlain : lblCipher), (encode ? plain : cipher) } },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GeneratePlayfair(QuizConfig config, string lang)
        {
            bool isCrypto = config.IsCryptanalysis;
            string key = GetRandomWord(5);
            var playfair = new PlayfairCipher(key);
            // Use SHORT meaningful text for Playfair Analysis
            string plain = isCrypto ? GetShortMeaningfulText(lang).Replace(" ", "").ToUpper() : GetRandomWord(6);
            // Playfair needs cleaning
            plain = plain.Replace("J", "I").Replace(" ", "");
            if (plain.Length % 2 != 0) plain += "X";
            
            string cipher = playfair.Encode(plain);
            string matrix = playfair.MatrixToString();

            // Localize Keys for Playfair
            var lblKey = lang == "en" ? "Keyword" : "Anahtar Kelime";
            var lblMatrix = lang == "en" ? "Matrix" : "Matris";
            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
            var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";

            if (isCrypto)
            {
                 var promptTrC = "Aşağıdaki metin Playfair ile şifrelenmiştir. Anahtar ve Matris verilmiştir. Şifreyi çözerek anlamlı metni bulunuz.";
                 var promptEnC = "Decrypt the Playfair ciphertext using the given Key/Matrix to find the meaningful text.";
                 
                 return new QuestionState
                 {
                    Topic = "Playfair",
                    Prompt = lang == "en" ? promptEnC : promptTrC,
                    InputHint = lang == "en" ? "Meaningful Text" : "Anlamlı Metin",
                    InputType = "text",
                    Data = new Dictionary<string, string> { { lblKey, key }, { lblMatrix, matrix }, { lblCipher, cipher } },
                    CorrectAnswer = plain,
                    Attempts = 0
                 };
            }

            bool encode = _random.Next(2) == 0;
            string decoded = playfair.Decode(cipher);

            var promptTr = encode
                ? $"'{key}' anahtar kelimesiyle oluşturulan Playfair matrisini kullanarak \"{plain}\" metnini şifreleyiniz."
                : "Aşağıdaki metin Playfair yöntemiyle şifrelenmiştir. Verilen anahtar kelime ve matrisi kullanarak şifreyi çözünüz.";
            var promptEn = encode
                ? $"Encrypt \"{plain}\" using Playfair cipher with key '{key}'."
                : "The text below is encrypted using Playfair cipher. Decrypt it using the key and matrix.";

            return new QuestionState
            {
                Topic = "Playfair",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = "Cevap",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string> { { lblKey, key }, { lblMatrix, matrix }, { lblPlain, plain } }
                    : new Dictionary<string, string> { { lblKey, key }, { lblMatrix, matrix }, { lblCipher, cipher } },
                CorrectAnswer = encode ? cipher : decoded,
                Attempts = 0
            };
        }

        private QuestionState GenerateTransposition(QuizConfig config, string lang)
        {
            bool isCrypto = config.IsCryptanalysis;
            string key = GetRandomWord(5);
            var trans = new TranspositionCipher(key);
            string plain = isCrypto ? GetMeaningfulText(lang).Replace(" ", "").ToUpper() : GetRandomWord(10);
            string cipher = trans.Encode(plain);

            var lblKey = lang == "en" ? "Keyword" : "Anahtar Kelime";
            var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
            var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";

            if (isCrypto)
            {
                // Find Key
                var promptTr = "Aşağıda düz metin ve şifreli hali verilmiştir. Sütunlu Yer Değiştirme (Transposition) şifrelemesinde kullanılan **Anahtar Kelimeyi** bulunuz.";
                var promptEn = "Find the **Keyword** used for Columnar Transposition given the plaintext and ciphertext.";
                
                return new QuestionState
                {
                    Topic = "Transposition",
                    Prompt = lang == "en" ? promptEn : promptTr,
                    InputHint = "Anahtar",
                    InputType = "text",
                    Data = new Dictionary<string, string> { { lblPlain, plain }, { lblCipher, cipher }, { lblIndices, GetAlphabetIndices() } },
                    CorrectAnswer = key,
                    Attempts = 0
                };
            }

            bool encode = _random.Next(2) == 0;
            var promptTr2 = encode ? $"\"{plain}\" metnini, '{key}' anahtarını kullanarak Sütunlu Yer Değiştirme yöntemiyle şifreleyiniz." : "Aşağıdaki metin Sütunlu Yer Değiştirme yöntemiyle şifrelenmiştir. Şifreyi çözünüz.";
            var promptEn2 = encode ? $"Encrypt \"{plain}\" using Columnar Transposition with key '{key}'." : "Decrypt the text below which was encrypted using Columnar Transposition.";

            return new QuestionState
            {
                Topic = "Transposition",
                Prompt = lang == "en" ? promptEn2 : promptTr2,
                InputHint = "Cevap",
                InputType = "text",
                Data = encode
                    ? new Dictionary<string, string> { { lblKey, key }, { lblPlain, plain }, { lblIndices, GetAlphabetIndices() } }
                    : new Dictionary<string, string> { { lblKey, key }, { lblCipher, cipher }, { lblIndices, GetAlphabetIndices() } },
                CorrectAnswer = encode ? cipher : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateGeneric(string topic, string lang)
        {
            return new QuestionState { Topic = topic, CorrectAnswer = "Bilinmiyor" }; 
        }

        // Helpers
        private string GetMeaningfulText(string lang)
        {
            var tr = new[] 
            {
                // History & Culture
                "TARIH GELECEGE ISIK TUTAN BIR AYNA GIBIDIR GECMISINI BILMEYENIN GELECEGI OLMAZ",
                "TURKIYENIN BASKENTI ANKARA COK KOKLU BIR GECMISE VE KULTURE SAHIP OLAN GUZEL BIR SEHIRDIR",
                "ISTANBUL BOGAZI ASYA VE AVRUPA KITALARINI BIRLESTIREN DUNYANIN EN ONEMLI SU YOLLARINDAN BIRIDIR",
                "ANADOLU TOPRAKLARI BINLERCE YILLIK MEDENIYETLERE EV SAHIPLIGI YAPMIS KADIM BIR COGRAFYADIR",
                "EFES ANTIK KENTI ROMA DONEMINDEN KALMA EN ONEMLI VE GORKEMLI MIMARI ESERLERDEN BIRIDIR",
                "GOBEKLITEPE INSANLIK TARIHININ BILINEN EN ESKI TAPINAK KOMPLEKSI OLARAK TARIHI DEGISTIRMISTIR",
                "CAPPADOCIA PERIBACALARI VE YERALTI SEHIRLERIYLE DUNYANIN EN ILGINC DOGAL OLUYUMLARINDAN BIRIDIR",
                "SUMELA MANASTIRI TRABZONDA SARP KAYALIKLARA INSA EDILMIS MUHTESEM BIR TARIHI YAPIDIR",
                
                // Science & Tech
                "BILIM VE TEKNOLOJI INSANLIGIN GELISIMI ICIN EN ONEMLI ARACLARDIR SUREKLI ILERLEMELIYIZ",
                "SIBER GUVENLIK GUNUMUZ DUNYASININ EN KRITIK SAVUNMA HATLARDINDAN BIRIDIR BILGI GUC DEMEKTIR",
                "YAPAY ZEKA TEKNOLOJILERI GELECEGIN MESLEKLERINI SEKILLENDIRIYOR ADAPTE OLMAK ZORUNDAYIZ",
                "KRIPTOLOJI VERILERIN GUVENLIGINI SAGLAMAK ICIN MATEMATIKSEL YONTEMLER KULLANAN BIR BILIM DALIDIR",
                "BLOCKCHAIN TEKNOLOJISI MERKEZIYETSIZ VE GUVENLI VERI SAKLAMA YONTEMLERI SUNMAKTADIR",
                "KUANTUM BILGISAYARLAR GELECEKTE SIFRELEME YONTEMLERINI KOKUNDEN DEGISTIRECEK BIR GUCE SAHIPTIR",
                "UZAY KESIFLERI INSANLIGIN EVRENI ANLAMASI VE YENI YASAM ALANLARI ARAMASI ICIN KRITIK ONEME SAHIPTIR",
                "YENILENEBILIR ENERJI KAYNAKLARI DUNYANIN GELECEGI ICIN SURDURULEBILIR BIR COZUM SUNMAKTADIR",

                // Proverbs & Wisdom
                "EGITIM SADECE OKULDA DEGIL HAYATIN HER ALANINDA DEVAM EDEN BIR SURECTIR OGRENMEK YASAMAKTIR",
                "SAKLA SAMANI GELIR ZAMANI DAMLAYA DAMLAYA GOL OLUR GUVENME VARLIGA DUSERSIN DARLIGA",
                "KESKIN SIRKE KUPUNE ZARAR ASLAN YATTIGI YERDEN BELLI OLUR BUGUNUN ISINI YARINA BIRAKMA",
                "BIR ELIN NESI VAR IKI ELIN SESI VAR BIRLIKTEN KUVVET DOGAR",
                "IYILIK YAP DENIZE AT BALIK BILMEZSE HALIK BILIR",
                "SABIR ACI ISE DE MEYVESI TATLIDIR HER GECENIN BIR SABAHI VARDIR",
                "DERDINI SOYLEMEYEN DERMAN BULAMAZ SORUNLARI PAYLASMAK COZUMUN YARISIDIR",
                "AKIL YASTA DEGIL BASTADIR ONEMLI OLAN TECRUBE DEGIL OGRENME KAPASITESIDIR", 
                "GUNES GIRMEYEN EVE DOKTOR GIRER SAGLIK EN BUYUK HAZINEDIR",
                "KITAPLAR HIC SOLMAYAN CICEKLERDIR OKUMAK ZIHNI ACAR VE RUHU BESLER"
            };
            var en = new[] 
            {
                // History & Culture
                "HISTORY IS LIKE A MIRROR THAT SHEDS LIGHT ON THE FUTURE THOSE WHO DO NOT KNOW THEIR PAST HAVE NO FUTURE",
                "LONDON IS THE CAPITAL OF ENGLAND AND IS A GLOBAL CITY WITH A RICH HISTORY AND DIVERSE CULTURE",
                "THE GREAT WALL OF CHINA IS ONE OF THE MOST IMPRESSIVE ARCHITECTURAL FEATS IN HUMAN HISTORY",
                "THE PYRAMIDS OF GIZA ARE THE ONLY SURVIVING WONDER OF THE ANCIENT WORLD AND REMAIN A MYSTERY",
                "THE RENAISSANCE WAS A PERIOD OF REBIRTH IN ART SCIENCE AND CULTURE THAT CHANGED EUROPE FOREVER",
                "SHAKESPEARE IS CONSIDERED THE GREATEST WRITER IN THE ENGLISH LANGUAGE AND HIS PLAYS ARE TIMELESS",
                "THE INDUSTRIAL REVOLUTION MARKED A MAJOR TURNING POINT IN HISTORY AFFECTING ALMOST EVERY ASPECT OF LIFE",
                "ANCIENT GREECE IS OFTEN CALLED THE CRADLE OF WESTERN CIVILIZATION FOR ITS CONTRIBUTIONS TO DEMOCRACY",

                // Science & Tech
                "SCIENCE AND TECHNOLOGY ARE THE MOST IMPORTANT TOOLS FOR THE DEVELOPMENT OF HUMANITY WE MUST CONSTANTLY PROGRESS",
                "CYBER SECURITY IS ONE OF THE MOST CRITICAL DEFENSE LINES OF TODAYS WORLD INFORMATION IS POWER",
                "ARTIFICIAL INTELLIGENCE TECHNOLOGIES ARE SHAPING THE PROFESSIONS OF THE FUTURE WE HAVE TO ADAPT",
                "CRYPTOLOGY IS A BRANCH OF SCIENCE THAT USES MATHEMATICAL METHODS TO ENSURE THE SECURITY OF DATA",
                "THE INTERNET HAS CHANGED THE WAY WE COMMUNICATE AND ACCESS INFORMATION IN REVOLUTIONARY WAYS",
                "SPACE EXPLORATION HELPS US UNDERSTAND OUR PLACE IN THE UNIVERSE AND PUSHES THE BOUNDARIES OF KNOWLEDGE",
                "RENEWABLE ENERGY SOURCES LIKE SOLAR AND WIND ARE ESSENTIAL FOR COMBATING CLIMATE CHANGE",
                "QUANTUM COMPUTING PROMISES TO SOLVE PROBLEMS THAT ARE CURRENTLY IMPOSSIBLE FOR CLASSICAL COMPUTERS",

                // Proverbs & Wisdom
                "ACTIONS SPEAK LOUDER THAN WORDS BETTER LATE THAN NEVER EASY COME EASY GO NO PAIN NO GAIN",
                "TIME IS MONEY KNOWLEDGE IS POWER HONESTY IS THE BEST POLICY PATIENCE IS A VIRTUE",
                "EDUCATION IS NOT JUST IN SCHOOL BUT A PROCESS THAT CONTINUES IN ALL AREAS OF LIFE LEARNING IS LIVING",
                "A JOURNEY OF A THOUSAND MILES BEGINS WITH A SINGLE STEP PERSEVERANCE IS THE KEY TO SUCCESS",
                "DONT COUNT YOUR CHICKENS BEFORE THEY HATCH WAIT UNTIL YOU ARE SURE OF THE OUTCOME",
                "WHEN IN ROME DO AS THE ROMANS DO RESPECT LOCAL CUSTOMS AND TRADITIONS",
                "THE PEN IS MIGHTIER THAN THE SWORD IDEAS CAN CHANGE THE WORLD MORE THAN VIOLENCE",
                "FORTUNE FAVORS THE BOLD THOSE WHO TAKE RISKS ARE OFTEN REWARDED",
                "HEALTH IS WEALTH WITHOUT IT ALL OTHER POSSESSIONS ARE MEANINGLESS",
                "BOOKS ARE PORTABLE MAGIC THAT ALLOW US TO TRAVEL WITHOUT MOVING OUR FEET"
            };
            var list = lang == "en" ? en : tr;
            return list[_random.Next(list.Length)];
        }

        private string GetShortMeaningfulText(string lang)
        {
            var tr = new[] { "SIBER VATAN", "GIZLI DOSYA", "DEVLET SIRRI", "GUVENLI HAT", "KOD ADI YESIL", "OPERASYON", "MAVI VATAN", "KRIPTO", "ISTIHBARAT" };
            var en = new[] { "TOP SECRET", "CYBER WAR", "SAFE ZONE", "CODE RED", "OPERATION", "INTELLIGENCE", "HIDDEN KEY", "SECRET FILE" };
            var list = lang == "en" ? en : tr;
            return list[_random.Next(list.Length)];
        }

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

        private string VigenereDecrypt(string input, string key)
        {
            string output = "";
            int keyIndex = 0;
            foreach (char c in input)
            {
                if (char.IsLetter(c))
                {
                    char d = char.IsUpper(c) ? 'A' : 'a';
                    int shift = char.ToUpper(key[keyIndex % key.Length]) - 'A';
                    // Decrypt: (C - Shift)
                    int p = (c - d - shift);
                    while(p < 0) p += 26;
                    output += (char)(p % 26 + d);
                    keyIndex++;
                }
                else output += c;
            }
            return output;
        }

        public QuestionState GenerateFromCustom(CustomQuestion cq, string language)
        {
            return cq.Topic switch
            {
                "Caesar" => GenerateCustomCaesar(cq, language),
                "Vigenere" => GenerateCustomVigenere(cq, language),
                "Base64" => GenerateCustomBase64(cq, language),
                "Xor" => GenerateCustomXor(cq, language),
                "Hill" => GenerateCustomHill(cq, language),
                "Monoalphabetic" => GenerateCustomMonoalphabetic(cq, language),
                "Playfair" => GenerateCustomPlayfair(cq, language),
                "Transposition" => GenerateCustomTransposition(cq, language),
                _ => GenerateCustomGeneric(cq, language)
            };
        }

        private QuestionState GenerateCustomBase64(CustomQuestion cq, string lang)
        {
            bool encode = cq.Mode == "Encrypt";
            string text = cq.Text;
            string plain = "";
            string encoded = "";

            if (encode)
            {
                plain = text;
                encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
            }
            else
            {
                 // Input is Base64, decode it
                 encoded = text;
                 try 
                 {
                    plain = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                 }
                 catch
                 {
                    plain = "INVALID BASE64";
                 }
            }

            var promptTr = encode ? $"\"{plain}\" metnini Base64 formatına kodlayınız." : "Aşağıda verilen Base64 kodlu metnin orijinal halini bulunuz.";
            var promptEn = encode ? $"Encode \"{plain}\" to Base64." : "Decode the following Base64 text.";
            var hintTr = encode ? "Encoded çıktıyı yazın" : "Düz metin";
            var hintEn = encode ? "Enter encoded output" : "Enter plaintext";

            var lblEncoded = lang == "en" ? "Encoded" : "Kodlanmış Metin";

            return new QuestionState
            {
                Topic = "Base64",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = lang == "en" ? hintEn : hintTr,
                InputType = "text",
                Data = encode ? new Dictionary<string, string>() : new Dictionary<string, string> { { lblEncoded, encoded } },
                CorrectAnswer = encode ? encoded : plain,
                Attempts = 0
            };
        }

        private QuestionState GenerateCustomXor(CustomQuestion cq, string lang)
        {
             // Text is Val 1, Key is Val 2
             int.TryParse(cq.Text, out int val1);
             int.TryParse(cq.Key, out int val2);
             int result = val1 ^ val2;

             var promptTr = $"Aşağıdaki {val1} ve {val2} değerlerinin XOR işleminin sonucunu onluk (decimal) tabanda yazınız.";
             var promptEn = $"Calculate XOR of {val1} and {val2} and write result in decimal.";
             var hintTr = "Sayı girin";
             var hintEn = "Enter number";

             var lblVal1 = lang == "en" ? "Value 1" : "Değer 1";
             var lblVal2 = lang == "en" ? "Value 2" : "Değer 2";

             return new QuestionState
             {
                Topic = "Xor",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = lang == "en" ? hintEn : hintTr,
                InputType = "number",
                Data = new Dictionary<string, string> { { lblVal1, val1.ToString() }, { lblVal2, val2.ToString() } },
                CorrectAnswer = result.ToString(),
                Attempts = 0
             };
        }

        private QuestionState GenerateCustomHill(CustomQuestion cq, string lang)
        {
            // Key is "a,b,c,d"
            var parts = cq.Key.Split(new[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries);
            int a=0, b=0, c=0, d=0;
            if(parts.Length >= 4)
            {
                int.TryParse(parts[0], out a);
                int.TryParse(parts[1], out b);
                int.TryParse(parts[2], out c);
                int.TryParse(parts[3], out d);
            }
            else { a=3; b=5; c=6; d=17; } // Default if parse fail

            var hill = new HillCipher(a, b, c, d);
            bool encode = cq.Mode == "Encrypt";
            // For custom, allow arbitrary length? Hill usually needs even length. 
            // We should pad if necessary or just use as is and let it throw if error?
            // HillCipher imp usually expects even. Let's pad with 'X' if odd.
            string text = cq.Text.Replace(" ", "").ToUpper();
            if (text.Length % 2 != 0) text += "X";

            string plain = "", cipher = "";
            string answer = "";

            if (encode) 
            {
                plain = text;
                cipher = hill.Encode(plain);
                answer = cipher;
            }
            else
            {
                cipher = text;
                try { plain = hill.Decode(cipher); } catch { plain = "ERROR"; }
                answer = plain;
            }

            var promptTr = encode
                ? "Aşağıda verilen anahtar matrisini kullanarak düz metni Hill şifreleme yöntemiyle şifreleyiniz."
                : "Aşağıdaki metin Hill yöntemiyle şifrelenmiştir. Verilen anahtar matrisini kullanarak şifreyi çözünüz.";
            var promptEn = encode
                ? "Encrypt the plaintext using Hill cipher with the given key matrix."
                : "Decrypt the ciphertext using Hill cipher with the given key matrix.";

            var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";
            var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";

            var data = new Dictionary<string, string> 
            { 
                 { "Matrix_00", a.ToString() }, { "Matrix_01", b.ToString() },
                 { "Matrix_10", c.ToString() }, { "Matrix_11", d.ToString() },
                 { lblIndices, GetAlphabetIndices() }
            };
            if(encode) data.Add(lblPlain, plain);
            else data.Add(lblCipher, cipher);

            return new QuestionState
            {
                Topic = "Hill",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = "Cevap",
                InputType = "text",
                Data = data,
                CorrectAnswer = answer,
                Attempts = 0
            };
        }

        private QuestionState GenerateCustomMonoalphabetic(CustomQuestion cq, string lang)
        {
             // Key is Keyword to generate mixed alphabet
             var mono = new MonoalphabeticCipher(cq.Key);
             bool encode = cq.Mode == "Encrypt";
             string text = cq.Text.ToUpper();
             string answer = "";
             
             if (encode) answer = mono.Encode(text);
             else answer = mono.Decode(text);

             var promptTr = encode
                ? $"Aşağıdaki karışık alfabe tablosunu kullanarak \"{text}\" metnini Monoalfabetik yöntemle şifreleyiniz."
                : "Aşağıdaki karışık alfabe tablosunu kullanarak şifrelenmiş metni çözünüz.";
             var promptEn = encode
                ? $"Encrypt \"{text}\" using the mixed alphabet table."
                : "Decrypt the ciphertext using the mixed alphabet table.";

             var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
             var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
             
             var data = new Dictionary<string, string> 
             {
                  { "MixedAlphabet", mono.MixedAlphabet }
             };
             if (encode) data.Add(lblPlain, text);
             else data.Add(lblCipher, text); // Input text is the question body

             return new QuestionState
             {
                Topic = "Monoalphabetic",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = "Cevap",
                InputType = "text",
                Data = data,
                CorrectAnswer = answer,
                Attempts = 0
             };
        }

        private QuestionState GenerateCustomPlayfair(CustomQuestion cq, string lang)
        {
             var playfair = new PlayfairCipher(cq.Key);
             bool encode = cq.Mode == "Encrypt";
             string text = cq.Text.ToUpper().Replace("J", "I").Replace(" ", "");
             // Playfair pair logic handled inside Cipher? Usually yes.
             string answer = "";
             string matrix = playfair.MatrixToString();

             if (encode) answer = playfair.Encode(text);
             else answer = playfair.Decode(text);

             var promptTr = encode
                ? $"'{cq.Key}' anahtar kelimesiyle oluşturulan Playfair matrisini kullanarak \"{text}\" metnini şifreleyiniz."
                : "Aşağıdaki metin Playfair yöntemiyle şifrelenmiştir. Verilen anahtar kelime ve matrisi kullanarak şifreyi çözünüz.";
             var promptEn = encode
                ? $"Encrypt \"{text}\" using Playfair cipher with key '{cq.Key}'."
                : "The text below is encrypted using Playfair cipher. Decrypt it using the key and matrix.";

              var lblKey = lang == "en" ? "Keyword" : "Anahtar Kelime";
              var lblMatrix = lang == "en" ? "Matrix" : "Matris";
              var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
              var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";

              var data = new Dictionary<string, string> 
              {
                   { lblKey, cq.Key },
                   { lblMatrix, matrix }
              };
              if (encode) data.Add(lblPlain, text);
              else data.Add(lblCipher, text);

             return new QuestionState
             {
                Topic = "Playfair",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = "Cevap",
                InputType = "text",
                Data = data,
                CorrectAnswer = answer,
                Attempts = 0
             };
        }

        private QuestionState GenerateCustomTransposition(CustomQuestion cq, string lang)
        {
            var trans = new TranspositionCipher(cq.Key);
            bool encode = cq.Mode == "Encrypt";
            string text = cq.Text.ToUpper().Replace(" ", "");
            string answer = "";

            if (encode) answer = trans.Encode(text);
            else answer = trans.Decode(text);

             var promptTr = encode ? $"\"{text}\" metnini, '{cq.Key}' anahtarını kullanarak Sütunlu Yer Değiştirme (Columnar Transposition) yöntemiyle şifreleyiniz." : "Aşağıdaki metin Sütunlu Yer Değiştirme (Columnar Transposition) yöntemiyle şifrelenmiştir. Şifreyi çözünüz.";
             var promptEn = encode ? $"Encrypt \"{text}\" using Columnar Transposition with key '{cq.Key}'." : "Decrypt the text below which was encrypted using Columnar Transposition.";

             var lblKey = lang == "en" ? "Keyword" : "Anahtar Kelime";
             var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
             var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
             var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";

             return new QuestionState
             {
                Topic = "Transposition",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = "Cevap",
                InputType = "text",
                Data = encode 
                     ? new Dictionary<string, string> { { lblKey, cq.Key }, { lblPlain, text }, { lblIndices, GetAlphabetIndices() } }
                     : new Dictionary<string, string> { { lblKey, cq.Key }, { lblCipher, text }, { lblIndices, GetAlphabetIndices() } },
                CorrectAnswer = answer,
                Attempts = 0
             };
        }

        private QuestionState GenerateCustomGeneric(CustomQuestion cq, string lang)
        {
            return new QuestionState 
            {
                Topic = cq.Topic,
                Prompt = cq.Text,
                InputHint = "Cevap",
                InputType = "text",
                CorrectAnswer = cq.Text,
                Attempts = 0
            };
        }

        private QuestionState GenerateCustomCaesar(CustomQuestion cq, string lang)
        {
            bool encode = cq.Mode == "Encrypt";
            int shift = int.TryParse(cq.Key, out int s) ? s : 3;
            string text = cq.Text;
            string answer = "";
            string plain = "";
            string cipher = "";

            if (encode)
            {
                plain = text;
                answer = CaesarCipher(plain, shift);
                cipher = answer;
            }
            else
            {
                cipher = text;
                answer = CaesarCipher(cipher, 26 - (shift % 26));
                plain = answer;
            }

            var promptTr = encode 
                ? $"\"{plain}\" metnini, {shift} birim öteleme kullanarak Sezar (Caesar) şifreleme yöntemiyle şifreleyiniz."
                : $"Aşağıdaki metin Sezar (Caesar) şifreleme yöntemiyle şifrelenmiştir (Öteleme: {shift}). Şifreyi çözerek orijinal metni bulunuz.";
            var promptEn = encode
                ? $"Encrypt the text \"{plain}\" using Caesar cipher with a shift of {shift}."
                : $"The text below is encrypted using Caesar cipher with a shift of {shift}. Decrypt it.";

            var hintTr = encode ? "Şifreli metni girin" : "Düz metni girin (Büyük harf)";
            var hintEn = encode ? "Enter encrypted text" : "Enter plaintext (Uppercase)";
            
            if (cq.IsAnalysis)
            {
                 // Analysis Mode: Hide Shift, User must find Shift and Plaintext.
                 // Custom Question key is the Shift.
                 promptTr = "Aşağıdaki şifreli metin Sezar (Caesar) yöntemiyle oluşturulmuştur. Şifreyi kırarak Shift değerini ve anlamlı metni bulunuz.";
                 promptEn = "The text below is encrypted using Caesar cipher. Crack the code to find the Shift value and the meaningful plaintext.";
                 hintTr = "Shift ve Düz Metin";
                 hintEn = "Shift and Plaintext";
            }

            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";
            var lblShift = lang == "en" ? "Shift" : "Kaydırma";
            var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";

            var data = new Dictionary<string, string>();
            if (cq.IsAnalysis)
            {
                 // Hide Shift
                 data.Add(lblCipher, cipher);
                 data.Add(lblIndices, GetAlphabetIndices());
            }
            else if (encode)
            {
                 data.Add(lblShift, $"+{shift}");
                 data.Add(lblIndices, GetAlphabetIndices());
            }
            else
            {
                 data.Add(lblCipher, cipher);
                 data.Add(lblShift, $"+{shift}");
                 data.Add(lblIndices, GetAlphabetIndices());
            }

            return new QuestionState
            {
                Topic = "Caesar",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = lang == "en" ? hintEn : hintTr,
                InputType = cq.IsAnalysis ? "caesar_analysis" : "text",
                Data = data,
                CorrectAnswer = cq.IsAnalysis ? $"{shift}|{plain}" : answer,
                Attempts = 0
            };
        }

        private QuestionState GenerateCustomVigenere(CustomQuestion cq, string lang)
        {
            bool encode = cq.Mode == "Encrypt";
            string key = cq.Key.ToUpper();
            string text = cq.Text;
            string answer = "";
            string plain = "";
            string cipher = "";

            if (encode)
            {
                plain = text;
                answer = VigenereCipher(plain, key);
                cipher = answer;
            }
            else
            {
                cipher = text;
                // Vigenere Decrypt: (C - K + 26) % 26
                // My VigenereCipher helper only encrypts. I need a decrypt logic or helper.
                // Let's implement decrypt here inline or add helper.
                answer = VigenereDecrypt(cipher, key);
                plain = answer;
            }

            var promptTr = encode
                ? $"\"{plain}\" metnini, '{key}' anahtar kelimesini kullanarak Vigenere şifreleme yöntemiyle şifreleyiniz."
                : $"Aşağıdaki metin '{key}' anahtarı kullanılarak Vigenere yöntemiyle şifrelenmiştir. Şifreyi çözünüz.";
            var promptEn = encode
                ? $"Encrypt \"{plain}\" using Vigenere cipher with keyword '{key}'."
                : $"Decrypt the text below which was encrypted using Vigenere cipher with keyword '{key}'.";

            var lblKey = lang == "en" ? "Keyword" : "Anahtar Kelime";
            var lblIndices = lang == "en" ? "Alphabet Indices" : "Alfabe İndeksleri";
            var lblPlain = lang == "en" ? "Plaintext" : "Düz Metin";
            var lblCipher = lang == "en" ? "Ciphertext" : "Şifreli Metin";

            var data = new Dictionary<string, string> 
            { 
                { lblKey, key },
                { lblIndices, GetAlphabetIndices() }
            };
            if (!encode) data.Add(lblCipher, cipher);
            if (encode) data.Add(lblPlain, plain);

            return new QuestionState
            {
                Topic = "Vigenere",
                Prompt = lang == "en" ? promptEn : promptTr,
                InputHint = lang == "en" ? (encode ? "Enter encrypted text" : "Enter plaintext") : (encode ? "Şifreli metni girin" : "Düz metni girin"),
                InputType = "text",
                Data = data,
                CorrectAnswer = answer,
                Attempts = 0
            };
        }



        private string GetAlphabetIndices()
        {
            return "A=0, B=1, C=2, D=3, E=4, F=5, G=6, H=7, I=8, J=9, K=10, L=11, M=12, N=13, O=14, P=15, Q=16, R=17, S=18, T=19, U=20, V=21, W=22, X=23, Y=24, Z=25";
        }
    }
}
