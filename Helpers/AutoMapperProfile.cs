using AutoMapper;
using FeedBackGeneratorApp.DTOs;

using UserModel = FeedBackGeneratorApp.Models.User;
using SurveyModel = FeedBackGeneratorApp.Models.Survey;
using QuestionModel = FeedBackGeneratorApp.Models.Question;
using SurveyResponseModel = FeedBackGeneratorApp.Models.SurveyResponse;
using AnswerModel = FeedBackGeneratorApp.Models.Answer;
using RecipientModel = FeedBackGeneratorApp.Models.Recipient;
using SurveyDistributionModel = FeedBackGeneratorApp.Models.SurveyDistribution;
using SurveyTemplateModel = FeedBackGeneratorApp.Models.SurveyTemplate;
using NotificationModel = FeedBackGeneratorApp.Models.Notification;

namespace FeedBackGeneratorApp.Helpers
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            // User Mappings
            CreateMap<UserModel, UserResponseDto>();
            CreateMap<RegisterDto, UserModel>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore());

            // Survey Mappings
            CreateMap<SurveyModel, SurveyResponseDto>()
                .ForMember(dest => dest.CreatedByUserName, opt => opt.MapFrom(src => src.CreatedByUser.FullName));
            CreateMap<CreateSurveyDto, SurveyModel>();

            // Question Mappings
            CreateMap<QuestionModel, QuestionResponseDto>();
            CreateMap<CreateQuestionDto, QuestionModel>();

            // SurveyResponse Mappings
            CreateMap<SurveyResponseModel, SurveyResponseDetailDto>()
                .ForMember(dest => dest.SurveyTitle, opt => opt.MapFrom(src => src.Survey.Title));

            // Answer Mappings
            CreateMap<AnswerModel, AnswerResponseDto>()
                .ForMember(dest => dest.QuestionText, opt => opt.MapFrom(src => src.Question.Text));
            CreateMap<SubmitAnswerDto, AnswerModel>();

            // Recipient Mappings
            CreateMap<RecipientModel, RecipientResponseDto>();
            CreateMap<CreateRecipientDto, RecipientModel>();

            // Distribution Mappings
            CreateMap<SurveyDistributionModel, DistributionResponseDto>()
                .ForMember(dest => dest.SurveyTitle, opt => opt.MapFrom(src => src.Survey.Title));
            CreateMap<CreateDistributionDto, SurveyDistributionModel>();

            // Template Mappings
            CreateMap<SurveyTemplateModel, TemplateResponseDto>();
            CreateMap<CreateTemplateDto, SurveyTemplateModel>();

            // Notification Mappings
            CreateMap<NotificationModel, NotificationResponseDto>();
        }
    }
}
