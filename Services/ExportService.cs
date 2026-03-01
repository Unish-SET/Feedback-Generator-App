using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Interfaces;
using System.Globalization;
using System.Text;

namespace FeedBackGeneratorApp.Services
{
    public class ExportService : IExportService
    {
        public byte[] ExportAnalyticsToCsv(SurveyAnalyticsDto analytics)
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8);
            using var csvWriter = new CsvWriter(streamWriter, new CsvConfiguration(CultureInfo.InvariantCulture));

            // Write Survey overview
            csvWriter.WriteField("Survey ID");
            csvWriter.WriteField(analytics.SurveyId);
            csvWriter.NextRecord();

            csvWriter.WriteField("Survey Title");
            csvWriter.WriteField(analytics.SurveyTitle);
            csvWriter.NextRecord();

            csvWriter.WriteField("Total Responses");
            csvWriter.WriteField(analytics.TotalResponses);
            csvWriter.NextRecord();

            csvWriter.WriteField("Completed Responses");
            csvWriter.WriteField(analytics.CompletedResponses);
            csvWriter.NextRecord();

            csvWriter.WriteField("Completion Rate (%)");
            csvWriter.WriteField(analytics.CompletionRate);
            csvWriter.NextRecord();
            csvWriter.NextRecord(); // Empty row

            // Write Questions
            csvWriter.WriteField("Question ID");
            csvWriter.WriteField("Question Text");
            csvWriter.WriteField("Question Type");
            csvWriter.WriteField("Total Answers");
            csvWriter.WriteField("Average Rating");
            csvWriter.WriteField("Responses / Distribution");
            csvWriter.NextRecord();

            foreach (var question in analytics.QuestionAnalytics)
            {
                csvWriter.WriteField(question.QuestionId);
                csvWriter.WriteField(question.QuestionText);
                csvWriter.WriteField(question.QuestionType);
                csvWriter.WriteField(question.TotalAnswers);
                csvWriter.WriteField(question.AverageRating?.ToString("0.0") ?? "N/A");

                if (question.QuestionType == "OpenText")
                {
                    var responses = string.Join(" | ", question.OpenTextResponses);
                    csvWriter.WriteField(responses);
                }
                else
                {
                    var distribution = string.Join(", ", question.AnswerDistribution.Select(kv => $"{kv.Key}: {kv.Value}"));
                    csvWriter.WriteField(distribution);
                }
                
                csvWriter.NextRecord();
            }

            streamWriter.Flush();
            return memoryStream.ToArray();
        }

        public byte[] ExportAnalyticsToExcel(SurveyAnalyticsDto analytics)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Survey Analytics");

            // Overview Section
            worksheet.Cell(1, 1).Value = "Survey Overview";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Range(1, 1, 1, 2).Merge();

            worksheet.Cell(2, 1).Value = "Survey ID";
            worksheet.Cell(2, 2).Value = analytics.SurveyId;

            worksheet.Cell(3, 1).Value = "Survey Title";
            worksheet.Cell(3, 2).Value = analytics.SurveyTitle;

            worksheet.Cell(4, 1).Value = "Total Responses";
            worksheet.Cell(4, 2).Value = analytics.TotalResponses;

            worksheet.Cell(5, 1).Value = "Completed Responses";
            worksheet.Cell(5, 2).Value = analytics.CompletedResponses;

            worksheet.Cell(6, 1).Value = "Incomplete Responses";
            worksheet.Cell(6, 2).Value = analytics.IncompleteResponses;

            worksheet.Cell(7, 1).Value = "Completion Rate (%)";
            worksheet.Cell(7, 2).Value = analytics.CompletionRate;

            worksheet.Range(2, 1, 7, 1).Style.Font.Bold = true;

            // Questions Section
            int row = 9;
            worksheet.Cell(row, 1).Value = "Question Analytics";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Style.Font.FontSize = 14;
            worksheet.Range(row, 1, row, 6).Merge();
            row++;

            // Headers
            worksheet.Cell(row, 1).Value = "Question ID";
            worksheet.Cell(row, 2).Value = "Question Text";
            worksheet.Cell(row, 3).Value = "Question Type";
            worksheet.Cell(row, 4).Value = "Total Answers";
            worksheet.Cell(row, 5).Value = "Average Rating";
            worksheet.Cell(row, 6).Value = "Responses / Distribution";
            worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightGray;
            row++;

            foreach (var question in analytics.QuestionAnalytics)
            {
                worksheet.Cell(row, 1).Value = question.QuestionId;
                worksheet.Cell(row, 2).Value = question.QuestionText;
                worksheet.Cell(row, 3).Value = question.QuestionType;
                worksheet.Cell(row, 4).Value = question.TotalAnswers;
                worksheet.Cell(row, 5).Value = question.AverageRating != null ? question.AverageRating.Value : "N/A";

                if (question.QuestionType == "OpenText")
                {
                    worksheet.Cell(row, 6).Value = string.Join(" | ", question.OpenTextResponses);
                }
                else
                {
                    worksheet.Cell(row, 6).Value = string.Join(", ", question.AnswerDistribution.Select(kv => $"{kv.Key}: {kv.Value}"));
                }
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
