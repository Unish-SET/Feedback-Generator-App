using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IQuestionImportService
    {
        /// <summary>
        /// Import questions from an Excel file.
        /// If surveyId is provided, questions are added to the latest Draft version.
        /// If addToQuestionBank is true, questions are also saved in the question bank.
        /// </summary>
        Task<QuestionImportResultDto> ImportAsync(
            Stream      fileStream,
            int?        surveyId,
            bool        addToQuestionBank,
            int         userId,
            string      role);

        /// <summary>Returns a pre-filled Excel template as a byte array.</summary>
        byte[] GetTemplate();
    }
}
