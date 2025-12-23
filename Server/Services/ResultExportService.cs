using CipherQuiz.Shared;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CipherQuiz.Server.Services
{
    public class ResultExportService
    {
        public FinalResultsDto BuildFinalResults(Room room)
        {
            var participants = room.Quiz.Values.Select(qs => new ParticipantResultDto
            {
                ParticipantId = qs.ParticipantId,
                DisplayName = qs.DisplayName,
                Completed = qs.Questions.Count(q => !string.IsNullOrEmpty(q.CorrectAnswer) == false), // CorrectAnswer cleared means solved? No, wait.
                // In QuizHub: "q.CorrectAnswer = ""; // Disable further answers" when solved.
                // But "q.RemainingScore = 0" if failed.
                // Let's use logic: Solved if CorrectAnswer is empty AND RemainingScore > 0?
                // Or just check if they are finished?
                // Actually, let's look at QuizHub.CheckIfFinished logic: string.IsNullOrEmpty(q.CorrectAnswer) || q.Attempts >= 3
                // Let's trust the Score for now.
                Total = qs.Questions.Count,
                Score = qs.Score,
                QuestionDetails = qs.Questions.Select(q => new QuestionResultDetailDto
                {
                    Topic = q.Topic,
                    Score = (string.IsNullOrEmpty(q.CorrectAnswer) && q.RemainingScore > 0) ? q.RemainingScore : 0, // If solved, score is awarded. If failed, 0.
                    // Wait, qs.Score is sum of awarded scores.
                    // If q.CorrectAnswer is empty, it means it was solved.
                    // If q.Attempts >= 3, it failed.
                    IsCorrect = string.IsNullOrEmpty(q.CorrectAnswer),
                    Attempts = q.Attempts
                }).ToList()
            }).OrderByDescending(x => x.Score).ToList();

            // Calculate Topic Stats
            var topicStats = new List<TopicStatDto>();
            if (room.Config.Topics != null)
            {
                foreach (var topic in room.Config.Topics)
                {
                    var allQuestionsOfTopic = room.Quiz.Values
                        .SelectMany(p => p.Questions)
                        .Where(q => q.Topic == topic)
                        .ToList();

                    if (allQuestionsOfTopic.Any())
                    {
                        var totalScore = allQuestionsOfTopic.Where(q => string.IsNullOrEmpty(q.CorrectAnswer)).Sum(q => q.RemainingScore); // Only solved ones have score? No, RemainingScore is modified.
                        // Actually, if solved, RemainingScore is the score awarded.
                        // If failed, RemainingScore is 0.
                        // If not attempted, RemainingScore is max.
                        // We need "Score Awarded".
                        // Let's assume if IsCorrect (CorrectAnswer empty), then RemainingScore is the score.
                        
                        // Better approach:
                        // Average Score = Total Score for Topic / Total Participants (or Total Questions?)
                        // Let's do Average Score per Question instance.
                        
                        double sumScore = 0;
                        int wrongs = 0;
                        int attempts = 0;

                        foreach (var q in allQuestionsOfTopic)
                        {
                            if (string.IsNullOrEmpty(q.CorrectAnswer)) sumScore += q.RemainingScore;
                            if (q.Attempts > 0) wrongs += q.Attempts; // Total wrong attempts
                            attempts += q.Attempts;
                        }

                        topicStats.Add(new TopicStatDto
                        {
                            Topic = topic,
                            AverageScore = sumScore / allQuestionsOfTopic.Count,
                            TotalWrongs = wrongs,
                            TotalAttempts = attempts
                        });
                    }
                }
            }

            return new FinalResultsDto
            {
                RoomCode = room.Code,
                RoomName = room.Name,
                StartedAtUtc = room.StartUtc ?? DateTime.UtcNow,
                GeneratedAtUtc = DateTime.UtcNow,
                DurationSeconds = room.Config.TimeLimitMinutes * 60,
                Topics = room.Config.Topics ?? new List<string>(),
                Participants = participants,
                TopicStats = topicStats
            };
        }

        public byte[] GenerateXlsx(FinalResultsDto data, bool detailed)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Sonuçlar");

            worksheet.Cell(1, 1).Value = "Oda Kodu";
            worksheet.Cell(1, 2).Value = data.RoomCode;
            worksheet.Cell(2, 1).Value = "Oda Adı";
            worksheet.Cell(2, 2).Value = data.RoomName;
            worksheet.Cell(3, 1).Value = "Tarih";
            worksheet.Cell(3, 2).Value = data.GeneratedAtUtc.ToString("g");

            var headerRow = worksheet.Row(5);
            headerRow.Cell(1).Value = "Sıra";
            headerRow.Cell(2).Value = "İsim";
            headerRow.Cell(3).Value = "Puan";
            headerRow.Cell(4).Value = "Tamamlanan";
            headerRow.Cell(5).Value = "Toplam";
            
            int col = 6;
            if (detailed)
            {
                foreach (var topic in data.Topics)
                {
                    headerRow.Cell(col++).Value = $"{topic} Puan";
                }
            }
            
            headerRow.Style.Font.Bold = true;

            int row = 6;
            int rank = 1;
            foreach (var p in data.Participants)
            {
                worksheet.Cell(row, 1).Value = rank++;
                worksheet.Cell(row, 2).Value = p.DisplayName;
                worksheet.Cell(row, 3).Value = p.Score;
                worksheet.Cell(row, 4).Value = p.Completed;
                worksheet.Cell(row, 5).Value = p.Total;

                if (detailed)
                {
                    int c = 6;
                    foreach (var topic in data.Topics)
                    {
                        // Sum score for this topic
                        var topicScore = p.QuestionDetails.Where(q => q.Topic == topic && q.IsCorrect).Sum(q => q.Score);
                        worksheet.Cell(row, c++).Value = topicScore;
                    }
                }

                row++;
            }

            worksheet.Columns().AdjustToContents();

            if (detailed)
            {
                var statsSheet = workbook.Worksheets.Add("İstatistikler");
                statsSheet.Cell(1, 1).Value = "Konu";
                statsSheet.Cell(1, 2).Value = "Ortalama Puan";
                statsSheet.Cell(1, 3).Value = "Toplam Hata";
                statsSheet.Cell(1, 4).Value = "Toplam Deneme";
                statsSheet.Row(1).Style.Font.Bold = true;

                int sRow = 2;
                foreach (var stat in data.TopicStats)
                {
                    statsSheet.Cell(sRow, 1).Value = stat.Topic;
                    statsSheet.Cell(sRow, 2).Value = stat.AverageScore;
                    statsSheet.Cell(sRow, 3).Value = stat.TotalWrongs;
                    statsSheet.Cell(sRow, 4).Value = stat.TotalAttempts;
                    sRow++;
                }
                statsSheet.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] GeneratePdf(FinalResultsDto data, bool detailed)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape()); // Landscape for more columns
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Text($"Cipher Quiz Sonuçları - {data.RoomName}")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(x =>
                        {
                            x.Item().Text($"Oda Kodu: {data.RoomCode}");
                            x.Item().Text($"Tarih: {data.GeneratedAtUtc:g}");
                            x.Item().Text($"Katılımcı Sayısı: {data.Participants.Count}");
                            
                            x.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(30);
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(50);
                                    columns.ConstantColumn(50);
                                    if (detailed)
                                    {
                                        foreach(var t in data.Topics) columns.RelativeColumn();
                                    }
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("#");
                                    header.Cell().Element(CellStyle).Text("İsim");
                                    header.Cell().Element(CellStyle).Text("Puan");
                                    header.Cell().Element(CellStyle).Text("D/Y"); // Completed/Total

                                    if (detailed)
                                    {
                                        foreach (var topic in data.Topics)
                                        {
                                            header.Cell().Element(CellStyle).Text(topic.Substring(0, Math.Min(3, topic.Length)));
                                        }
                                    }

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                    }
                                });

                                int rank = 1;
                                foreach (var p in data.Participants)
                                {
                                    table.Cell().Element(CellStyle).Text(rank++.ToString());
                                    table.Cell().Element(CellStyle).Text(p.DisplayName);
                                    table.Cell().Element(CellStyle).Text(p.Score.ToString("F0"));
                                    table.Cell().Element(CellStyle).Text($"{p.Completed}/{p.Total}");

                                    if (detailed)
                                    {
                                        foreach (var topic in data.Topics)
                                        {
                                            var topicScore = p.QuestionDetails.Where(q => q.Topic == topic && q.IsCorrect).Sum(q => q.Score);
                                            table.Cell().Element(CellStyle).Text(topicScore.ToString("F0"));
                                        }
                                    }

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                    }
                                }
                            });

                            if (detailed && data.TopicStats.Any())
                            {
                                x.Item().PageBreak();
                                x.Item().Text("İstatistikler").SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);
                                x.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderStyle).Text("Konu");
                                        header.Cell().Element(HeaderStyle).Text("Ortalama Puan");
                                        header.Cell().Element(HeaderStyle).Text("Toplam Hata");
                                        header.Cell().Element(HeaderStyle).Text("Toplam Deneme");

                                        static IContainer HeaderStyle(IContainer container)
                                        {
                                            return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                        }
                                    });

                                    foreach (var stat in data.TopicStats)
                                    {
                                        table.Cell().Element(CellStyle).Text(stat.Topic);
                                        table.Cell().Element(CellStyle).Text(stat.AverageScore.ToString("F1"));
                                        table.Cell().Element(CellStyle).Text(stat.TotalWrongs.ToString());
                                        table.Cell().Element(CellStyle).Text(stat.TotalAttempts.ToString());

                                        static IContainer CellStyle(IContainer container)
                                        {
                                            return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                        }
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                        });
                });
            });

            return doc.GeneratePdf();
        }

        public byte[] GenerateFullDetailsPdf(Room room)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header()
                        .Background(Colors.Blue.Darken2)
                        .Padding(20)
                        .Row(row => 
                        {
                            row.RelativeItem().Column(c => 
                            {
                                c.Item().Text($"Detaylı Sınav Raporu").FontSize(24).SemiBold().FontColor(Colors.White);
                                c.Item().Text($"{room.Name} ({room.Code})").FontSize(14).FontColor(Colors.Grey.Lighten4);
                            });
                            row.AutoItem().AlignMiddle().Text(DateTime.Now.ToString("g")).FontColor(Colors.White).AlignRight();
                        });



                    page.Content().PaddingVertical(20).Column(col =>
                    {


                        // --- Leaderboard Section ---
                        col.Item().Text("Genel Sıralama").FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn();
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("#");
                                header.Cell().Element(HeaderStyle).Text("İsim");
                                header.Cell().Element(HeaderStyle).Text("Puan");
                                header.Cell().Element(HeaderStyle).Text("Doğru/Toplam");

                                static IContainer HeaderStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                }
                            });

                            int rank = 1;
                            // Need to calculate scores properly if not already in room.Participants
                            // room.Participants has basic info, room.Quiz has scores.
                            var rankedParticipants = room.Participants
                                .Select(p => {
                                    var state = room.Quiz.ContainsKey(p.ParticipantId) ? room.Quiz[p.ParticipantId] : null;
                                    var correctCount = state?.Questions.Count(q => q.IsSolved) ?? 0;
                                    var totalQuestions = state?.Questions.Count ?? 0;
                                    
                                    return new 
                                    { 
                                        p.DisplayName, 
                                        Score = state?.Score ?? 0,
                                        Correct = correctCount,
                                        Total = totalQuestions
                                    };
                                })
                                .OrderByDescending(p => p.Score)
                                .ToList();

                            foreach (var p in rankedParticipants)
                            {
                                table.Cell().Element(CellStyle).Text(rank++.ToString());
                                table.Cell().Element(CellStyle).Text(p.DisplayName);
                                table.Cell().Element(CellStyle).Text(p.Score.ToString("F0"));
                                table.Cell().Element(CellStyle).Text($"{p.Correct}/{p.Total}");

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                                }
                            }
                        });

                        foreach (var participant in room.Participants)
                        {
                            if (!room.Quiz.TryGetValue(participant.ParticipantId, out var pState)) continue;

                            col.Item().PageBreak();
                            
                            // Participant Header
                            col.Item().Background(Colors.Grey.Lighten4).BorderBottom(2).BorderColor(Colors.Blue.Medium).Padding(15).Row(row => 
                            {
                                row.RelativeItem().Column(c => 
                                {
                                    c.Item().Text(participant.DisplayName).FontSize(18).Bold().FontColor(Colors.Black);
                                    c.Item().Text($"ID: {participant.ParticipantId}").FontSize(8).FontColor(Colors.Grey.Darken1);
                                });
                                row.AutoItem().Column(c => 
                                {
                                    c.Item().Text("Toplam Puan").FontSize(10).AlignRight().FontColor(Colors.Grey.Darken2);
                                    c.Item().Text($"{pState.Score:F0}").FontSize(20).Bold().FontColor(Colors.Green.Darken1).AlignRight();
                                });
                            });
                            
                            col.Item().PaddingBottom(15);

                            foreach (var q in pState.Questions.OrderBy(x => x.Position))
                            {
                                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.White).Column(qCol =>
                                {
                                    // Question Header
                                    qCol.Item().Background(Colors.Blue.Lighten5).Padding(10).Row(row =>
                                    {
                                        row.RelativeItem().Text($"Soru {q.Position}: {q.Topic}").SemiBold().FontSize(11).FontColor(Colors.Blue.Darken2);
                                        row.AutoItem().Text($"Puan: {(q.IsSolved ? q.RemainingScore : 0):F0} / {q.RemainingScore:F0}").FontSize(10).FontColor(q.IsSolved ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();
                                    });

                                    qCol.Item().Padding(15).Column(content => 
                                    {
                                        content.Item().Text(q.Prompt).FontSize(11).Italic().FontColor(Colors.Grey.Darken3);
                                        content.Item().PaddingBottom(10);

                                        // Visual Data Rendering
                                        if (q.Data != null)
                                        {
                                            content.Item().Background(Colors.Grey.Lighten5).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten3).Column(dCol =>
                                            {
                                                // Playfair Matrix
                                                if (q.Data.ContainsKey("Matris"))
                                                {
                                                    var matrix = q.Data["Matris"];
                                                    var key = q.Data.ContainsKey("Anahtar Kelime") ? q.Data["Anahtar Kelime"] : "";
                                                    dCol.Item().Text("Playfair Matrisi").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                                                    dCol.Item().PaddingTop(5).Table(table =>
                                                    {
                                                        table.ColumnsDefinition(c => 
                                                        {
                                                            for(int i=0; i<5; i++) c.ConstantColumn(20);
                                                        });
                                                        
                                                        var rows = matrix.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                                        foreach (var row in rows)
                                                        {
                                                            foreach (var ch in row.Trim())
                                                            {
                                                                bool isKey = !string.IsNullOrEmpty(key) && (key.ToUpper().Contains(ch) || (ch == 'I' && key.ToUpper().Contains('J')));
                                                                table.Cell().Border(1).BorderColor(Colors.Grey.Medium)
                                                                    .Background(isKey ? Colors.Yellow.Lighten3 : Colors.White)
                                                                    .AlignCenter().AlignMiddle().Height(20)
                                                                    .Text(ch == 'I' ? "I/J" : ch.ToString()).FontSize(9);
                                                            }
                                                        }
                                                    });
                                                    dCol.Item().PaddingBottom(5);
                                                }

                                                // Hill Matrix
                                                if (q.Data.ContainsKey("Matrix_00"))
                                                {
                                                    dCol.Item().Text("Anahtar Matrisi").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                                                    dCol.Item().PaddingTop(5).Table(table =>
                                                    {
                                                        table.ColumnsDefinition(c => { c.ConstantColumn(25); c.ConstantColumn(25); });
                                                        
                                                        table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.White).AlignCenter().AlignMiddle().Height(25).Text(q.Data["Matrix_00"]);
                                                        table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.White).AlignCenter().AlignMiddle().Height(25).Text(q.Data["Matrix_01"]);
                                                        table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.White).AlignCenter().AlignMiddle().Height(25).Text(q.Data["Matrix_10"]);
                                                        table.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.White).AlignCenter().AlignMiddle().Height(25).Text(q.Data["Matrix_11"]);
                                                    });
                                                    dCol.Item().PaddingBottom(5);
                                                }

                                                // Mixed Alphabet
                                                if (q.Data.ContainsKey("MixedAlphabet"))
                                                {
                                                    dCol.Item().Text("Alfabe Eşleşmesi").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                                                    dCol.Item().PaddingTop(2).Text("A B C D E F G H I J K L M N O P Q R S T U V W X Y Z").FontFamily("Courier New").FontSize(8).FontColor(Colors.Grey.Darken2);
                                                    dCol.Item().Text(string.Join(" ", q.Data["MixedAlphabet"].ToCharArray())).FontFamily("Courier New").FontSize(8).FontColor(Colors.Blue.Medium).SemiBold();
                                                    dCol.Item().PaddingBottom(5);
                                                }

                                                // Other Data
                                                foreach (var kvp in q.Data)
                                                {
                                                    if (kvp.Key == "Matris" || kvp.Key.StartsWith("Matrix_") || kvp.Key == "MixedAlphabet") continue;
                                                    dCol.Item().Row(r => 
                                                    {
                                                        r.ConstantItem(100).Text($"{kvp.Key}:").FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                                                        r.RelativeItem().Text(kvp.Value).FontSize(9).FontFamily("Courier New");
                                                    });
                                                }
                                            });
                                            content.Item().PaddingBottom(10);
                                        }

                                        content.Item().Row(ansRow =>
                                        {
                                            ansRow.RelativeItem().Column(c =>
                                            {
                                                c.Item().Text("Verilen Cevap".ToUpper()).FontSize(8).SemiBold().FontColor(Colors.Grey.Darken1);
                                                c.Item().Border(1).BorderColor(q.IsSolved ? Colors.Green.Lighten2 : Colors.Red.Lighten2)
                                                    .Background(q.IsSolved ? Colors.Green.Lighten5 : Colors.Red.Lighten5)
                                                    .Padding(8)
                                                    .Text(string.IsNullOrEmpty(q.UserAnswer) ? "-" : q.UserAnswer).FontSize(10).FontFamily("Courier New");
                                            });
                                            
                                            ansRow.ConstantItem(15);

                                            ansRow.RelativeItem().Column(c =>
                                            {
                                                c.Item().Text("Doğru Cevap".ToUpper()).FontSize(8).SemiBold().FontColor(Colors.Grey.Darken1);
                                                c.Item().Border(1).BorderColor(Colors.Blue.Lighten2)
                                                    .Background(Colors.Blue.Lighten5)
                                                    .Padding(8)
                                                    .Text(q.CorrectAnswer).FontSize(10).FontFamily("Courier New");
                                            });
                                        });

                                        content.Item().PaddingTop(5).Row(statRow =>
                                        {
                                            statRow.RelativeItem().Text($"Deneme Sayısı: {q.Attempts}").FontSize(8).FontColor(Colors.Grey.Darken1);
                                        });
                                    });
                                });
                                col.Item().Height(15);
                            }


                            // Proctoring Logs Section
                            if (room.ProctorLogs.TryGetValue(participant.ParticipantId, out var logs) && logs.Any())
                            {
                                col.Item().PageBreak();
                                col.Item().Background(Colors.Red.Lighten5).BorderBottom(2).BorderColor(Colors.Red.Medium).Padding(15).Row(row => 
                                {
                                    row.RelativeItem().Text("Güvenlik & Kopya Kayıtları").FontSize(16).Bold().FontColor(Colors.Red.Darken2);
                                    row.AutoItem().Text($"{logs.Count} Olay").FontSize(10).FontColor(Colors.Red.Darken2).SemiBold();
                                });

                                col.Item().PaddingTop(10).Table(table =>
                                {
                                    table.ColumnsDefinition(c => 
                                    {
                                        c.ConstantColumn(100); // Time
                                        c.RelativeColumn();    // Type
                                        c.RelativeColumn(2);   // Content
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderStyle).Text("Zaman");
                                        header.Cell().Element(HeaderStyle).Text("Olay Türü");
                                        header.Cell().Element(HeaderStyle).Text("Detay");

                                        static IContainer HeaderStyle(IContainer container)
                                        {
                                            return container.DefaultTextStyle(x => x.SemiBold().FontColor(Colors.Red.Darken3)).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Red.Medium);
                                        }
                                    });

                                    foreach (var log in logs.OrderBy(l => l.TimestampUtc))
                                    {
                                        var type = log.Type;
                                        var content = log.Content;

                                        // Translation for legacy/raw logs
                                        if (type == "visibility") { type = "Sekme Durumu"; content = content == "hidden" ? "Sekme Alta Alındı / Gizlendi" : "Sekme Tekrar Açıldı"; }
                                        else if (type == "blur") { type = "Odak Durumu"; content = "Pencere Odak Kaybı"; }
                                        else if (type == "focus") { type = "Odak Durumu"; content = "Pencere Odağı Geri Geldi"; }
                                        else if (type == "copy") { type = "Kopyalama Denemesi"; content = "Engellendi"; }
                                        else if (type == "paste") { type = "Yapıştırma Denemesi"; content = "Engellendi"; }
                                        else if (type == "contextmenu") { type = "Sağ Tık Denemesi"; content = "Engellendi"; }
                                        else if (type == "fullscreenchange") { type = "Ekran Durumu"; content = "Tam Ekran Değişikliği"; }

                                        table.Cell().Element(CellStyle).Text(log.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"));
                                        table.Cell().Element(CellStyle).Text(type);
                                        table.Cell().Element(CellStyle).Text(content);

                                        static IContainer CellStyle(IContainer container)
                                        {
                                            return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).DefaultTextStyle(x => x.FontSize(9));
                                        }
                                    }
                                });
                            }
                        }

                        // --- General Security Summary Section (Moved to End) ---
                        col.Item().PageBreak();
                        
                        // Check for both new descriptive types and old raw types
                        var fullscreenViolators = room.ProctorLogs.Count(kvp => kvp.Value.Any(e => 
                            (e.Type == "Ekran Durumu" && e.Content.Contains("Çıkıldı")) || 
                            (e.Type == "fullscreenchange" && e.Content.Contains("Exit")))); 
                        
                        var tabViolators = room.ProctorLogs.Count(kvp => kvp.Value.Any(e => 
                            (e.Type == "Sekme Durumu" && e.Content.Contains("Gizlendi")) || 
                            (e.Type == "Odak Durumu" && e.Content.Contains("Kaybı")) ||
                            e.Type == "visibility" || e.Type == "blur"));

                        var copyViolators = room.ProctorLogs.Count(kvp => kvp.Value.Any(e => 
                            e.Type.Contains("Kopyalama") || e.Type.Contains("Yapıştırma") || e.Type.Contains("Sağ Tık") || e.Type.Contains("Geliştirici") ||
                            e.Type == "copy" || e.Type == "paste" || e.Type == "contextmenu"));

                        var totalEvents = room.ProctorLogs.Sum(x => x.Value.Count);

                        col.Item().Background(Colors.Grey.Lighten5).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(20).Column(summary => 
                        {
                            summary.Item().Row(r => 
                            {
                                r.RelativeItem().Text("Genel Güvenlik Özeti").FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                                r.AutoItem().Text($"{room.Participants.Count} Katılımcı / {totalEvents} Olay").FontSize(10).FontColor(Colors.Grey.Darken2);
                            });
                            
                            summary.Item().PaddingTop(15).Table(table =>
                            {
                                table.ColumnsDefinition(columns => 
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });
                                
                                // Card 1: Fullscreen
                                table.Cell().Background(Colors.White).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(10).Column(c => 
                                {
                                    c.Item().Text("Tam Ekran İhlali").FontSize(10).SemiBold().FontColor(Colors.Orange.Darken3);
                                    c.Item().Text($"{fullscreenViolators} Kişi").FontSize(18).Bold().FontColor(Colors.Black);
                                    c.Item().Text("Tam ekrandan çıkış yaptı.").FontSize(8).FontColor(Colors.Grey.Darken1);
                                });

                                // Card 2: Tab/Focus
                                table.Cell().Background(Colors.White).Border(1).BorderColor(Colors.Yellow.Darken1).Padding(10).Column(c => 
                                {
                                    c.Item().Text("Odak / Sekme İhlali").FontSize(10).SemiBold().FontColor(Colors.Yellow.Darken3);
                                    c.Item().Text($"{tabViolators} Kişi").FontSize(18).Bold().FontColor(Colors.Black);
                                    c.Item().Text("Sekme değiştirdi veya alta aldı.").FontSize(8).FontColor(Colors.Grey.Darken1);
                                });

                                // Card 3: Copy/Paste
                                table.Cell().Background(Colors.White).Border(1).BorderColor(Colors.Red.Lighten3).Padding(10).Column(c => 
                                {
                                    c.Item().Text("Kopyalama Girişimi").FontSize(10).SemiBold().FontColor(Colors.Red.Darken3);
                                    c.Item().Text($"{copyViolators} Kişi").FontSize(18).Bold().FontColor(Colors.Black);
                                    c.Item().Text("Kopyalama/Yapıştırma denedi.").FontSize(8).FontColor(Colors.Grey.Darken1);
                                });
                            });
                        });

                        // --- Consolidated Security Logs Table ---
                        col.Item().PaddingTop(20).Text("Tüm Güvenlik Kayıtları").FontSize(14).Bold().FontColor(Colors.Black);
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(60); // Time
                                columns.RelativeColumn();   // User
                                columns.ConstantColumn(100); // Type
                                columns.RelativeColumn(2);  // Detail
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).Text("Zaman");
                                header.Cell().Element(HeaderStyle).Text("Kullanıcı");
                                header.Cell().Element(HeaderStyle).Text("Olay Türü");
                                header.Cell().Element(HeaderStyle).Text("Detay");

                                static IContainer HeaderStyle(IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                }
                            });

                            var allLogs = room.ProctorLogs
                                .SelectMany(kvp => kvp.Value.Select(log => new { 
                                    ParticipantId = kvp.Key, 
                                    Log = log,
                                    DisplayName = room.Participants.FirstOrDefault(p => p.ParticipantId == kvp.Key)?.DisplayName ?? "Bilinmeyen"
                                }))
                                .OrderBy(x => x.Log.TimestampUtc)
                                .ToList();

                            foreach (var item in allLogs)
                            {
                                var type = item.Log.Type;
                                var content = item.Log.Content;

                                // Translation
                                if (type == "visibility") { type = "Sekme Durumu"; content = content == "hidden" ? "Sekme Alta Alındı / Gizlendi" : "Sekme Tekrar Açıldı"; }
                                else if (type == "blur") { type = "Odak Durumu"; content = "Pencere Odak Kaybı"; }
                                else if (type == "focus") { type = "Odak Durumu"; content = "Pencere Odağı Geri Geldi"; }
                                else if (type == "copy") { type = "Kopyalama Denemesi"; content = "Engellendi"; }
                                else if (type == "paste") { type = "Yapıştırma Denemesi"; content = "Engellendi"; }
                                else if (type == "contextmenu") { type = "Sağ Tık Denemesi"; content = "Engellendi"; }
                                else if (type == "fullscreenchange") { type = "Ekran Durumu"; content = "Tam Ekran Değişikliği"; }

                                table.Cell().Element(CellStyle).Text(item.Log.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"));
                                table.Cell().Element(CellStyle).Text(item.DisplayName);
                                table.Cell().Element(CellStyle).Text(type);
                                table.Cell().Element(CellStyle).Text(content);

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).DefaultTextStyle(x => x.FontSize(9));
                                }
                            }
                        });
                    });

                    page.Footer()
                        .PaddingTop(10)
                        .Row(row => 
                        {
                            row.RelativeItem().Text("Cipher Quiz System").FontSize(8).FontColor(Colors.Grey.Medium);
                            row.AutoItem().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                        });
                });


            });

            return doc.GeneratePdf();
        }
    }
}
