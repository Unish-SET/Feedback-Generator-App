using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Context
{
    public class FeedBackDbContext : DbContext
    {
        public FeedBackDbContext(DbContextOptions<FeedBackDbContext> options) : base(options) { }

        public DbSet<User>           Users           => Set<User>();
        public DbSet<Survey>         Surveys         => Set<Survey>();
        public DbSet<SurveyVersion>  SurveyVersions  => Set<SurveyVersion>();
        public DbSet<Question>       Questions       => Set<Question>();
        public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
        public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
        public DbSet<Answer>         Answers         => Set<Answer>();
        public DbSet<AuditLog>       AuditLogs       => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── User ──
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Role).HasConversion<int>();
            });

            // ── Survey ──
            modelBuilder.Entity<Survey>(entity =>
            {
                entity.HasIndex(s => s.PublicToken).IsUnique();
                entity.Property(s => s.Status).HasConversion<int>();
                entity.HasQueryFilter(s => !s.IsDeleted);

                entity.HasOne(s => s.Creator)
                      .WithMany(u => u.Surveys)
                      .HasForeignKey(s => s.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── SurveyVersion ──
            modelBuilder.Entity<SurveyVersion>(entity =>
            {
                entity.HasQueryFilter(sv => !sv.Survey.IsDeleted);

                entity.HasOne(sv => sv.Survey)
                      .WithMany(s => s.Versions)
                      .HasForeignKey(sv => sv.SurveyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Question ──
            modelBuilder.Entity<Question>(entity =>
            {
                entity.Property(q => q.Type).HasConversion<int>();
                entity.HasQueryFilter(q => !q.SurveyVersion.Survey.IsDeleted);

                entity.HasOne(q => q.SurveyVersion)
                      .WithMany(sv => sv.Questions)
                      .HasForeignKey(q => q.SurveyVersionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── QuestionOption ──
            modelBuilder.Entity<QuestionOption>(entity =>
            {
                entity.HasQueryFilter(qo => !qo.Question.SurveyVersion.Survey.IsDeleted);

                entity.HasOne(qo => qo.Question)
                      .WithMany(q => q.Options)
                      .HasForeignKey(qo => qo.QuestionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── SurveyResponse ──
            modelBuilder.Entity<SurveyResponse>(entity =>
            {
                entity.HasQueryFilter(sr => !sr.SurveyVersion.Survey.IsDeleted);

                entity.HasIndex(sr => new { sr.SurveyVersionId, sr.UserId })
                      .IsUnique()
                      .HasFilter("[UserId] IS NOT NULL");

                entity.HasOne(sr => sr.SurveyVersion)
                      .WithMany(sv => sv.Responses)
                      .HasForeignKey(sr => sr.SurveyVersionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sr => sr.User)
                      .WithMany(u => u.Responses)
                      .HasForeignKey(sr => sr.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Answer ──
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.HasQueryFilter(a => !a.Response.SurveyVersion.Survey.IsDeleted);

                entity.HasOne(a => a.Response)
                      .WithMany(sr => sr.Answers)
                      .HasForeignKey(a => a.ResponseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Question)
                      .WithMany(q => q.Answers)
                      .HasForeignKey(a => a.QuestionId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(a => a.SelectedOption)
                      .WithMany()
                      .HasForeignKey(a => a.SelectedOptionId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ── AuditLog ──
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(a => new { a.EntityName, a.EntityId });
                entity.HasIndex(a => a.UserId);
                entity.HasIndex(a => a.Timestamp);
                entity.HasIndex(a => a.CorrelationId);
            });
        }
    }
}
