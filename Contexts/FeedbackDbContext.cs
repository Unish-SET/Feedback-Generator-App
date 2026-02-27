using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Contexts
{
    public class FeedbackDbContext : DbContext
    {
        public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Survey> Surveys { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<SurveyDistribution> SurveyDistributions { get; set; }
        public DbSet<Recipient> Recipients { get; set; }
        public DbSet<SurveyResponse> SurveyResponses { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<SurveyTemplate> SurveyTemplates { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // Survey
            modelBuilder.Entity<Survey>(entity =>
            {
                entity.HasOne(s => s.CreatedByUser)
                      .WithMany(u => u.Surveys)
                      .HasForeignKey(s => s.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Question
            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasOne(q => q.Survey)
                      .WithMany(s => s.Questions)
                      .HasForeignKey(q => q.SurveyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // SurveyDistribution
            modelBuilder.Entity<SurveyDistribution>(entity =>
            {
                entity.HasOne(d => d.Survey)
                      .WithMany(s => s.SurveyDistributions)
                      .HasForeignKey(d => d.SurveyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Recipient
            modelBuilder.Entity<Recipient>(entity =>
            {
                entity.HasOne(r => r.CreatedByUser)
                      .WithMany(u => u.Recipients)
                      .HasForeignKey(r => r.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // SurveyResponse
            modelBuilder.Entity<SurveyResponse>(entity =>
            {
                entity.HasOne(sr => sr.Survey)
                      .WithMany(s => s.SurveyResponses)
                      .HasForeignKey(sr => sr.SurveyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sr => sr.RespondentUser)
                      .WithMany(u => u.SurveyResponses)
                      .HasForeignKey(sr => sr.RespondentUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Answer
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.HasOne(a => a.SurveyResponse)
                      .WithMany(sr => sr.Answers)
                      .HasForeignKey(a => a.SurveyResponseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Question)
                      .WithMany(q => q.Answers)
                      .HasForeignKey(a => a.QuestionId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // RefreshToken
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasOne(rt => rt.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(rt => rt.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(rt => rt.Token).IsUnique();
            });
        }
    }
}
