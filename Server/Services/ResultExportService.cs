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
                DurationSeconds = room.Config.DurationSeconds,
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
    }
}
