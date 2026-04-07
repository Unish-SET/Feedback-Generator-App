using FeedBackApp.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBackApp.Context
{
    public class FeedBackDbContext : DbContext
    {


        public FeedBackDbContext(DbContextOptions<FeedBackDbContext> options) : base(options) { }

        public DbSet<User>               Users               => Set<User>();
        public DbSet<Survey>             Surveys             => Set<Survey>();
        public DbSet<Question>           Questions           => Set<Question>();
        public DbSet<QuestionOption>     QuestionOptions     => Set<QuestionOption>();
        public DbSet<SurveyResponse>     SurveyResponses     => Set<SurveyResponse>();
        public DbSet<Answer>             Answers             => Set<Answer>();
        public DbSet<AuditLog>           AuditLogs           => Set<AuditLog>();
        public DbSet<BankQuestion>       BankQuestions       => Set<BankQuestion>();
        public DbSet<BankQuestionOption> BankQuestionOptions => Set<BankQuestionOption>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── User ──
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.IsActive);
                entity.HasIndex(u => u.IsDeleted);
                entity.Property(u => u.Role).HasConversion<int>();
            });

            // ── Survey ──
            modelBuilder.Entity<Survey>(entity =>
            {
                entity.HasIndex(s => s.PublicToken).IsUnique();
                entity.HasIndex(s => s.IsDeleted);
                entity.HasIndex(s => s.State);
                entity.HasIndex(s => s.CreatedAt);
                entity.Property(s => s.State).HasConversion<int>();
                entity.HasQueryFilter(s => !s.IsDeleted);

                entity.HasOne(s => s.Creator)
                      .WithMany(u => u.Surveys)
                      .HasForeignKey(s => s.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Question ──
            modelBuilder.Entity<Question>(entity =>
            {
                entity.Property(q => q.Type).HasConversion<int>();
                entity.HasQueryFilter(q => !q.Survey.IsDeleted);

                entity.HasOne(q => q.Survey)
                      .WithMany(s => s.Questions)
                      .HasForeignKey(q => q.SurveyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── QuestionOption ──
            modelBuilder.Entity<QuestionOption>(entity =>
            {
                entity.HasQueryFilter(qo => Questions.Any(q => q.Id == qo.QuestionId));

                entity.HasOne(qo => qo.Question)
                      .WithMany(q => q.Options)
                      .HasForeignKey(qo => qo.QuestionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── SurveyResponse ──
            modelBuilder.Entity<SurveyResponse>(entity =>
            {
                entity.HasQueryFilter(sr => !sr.Survey.IsDeleted);

                entity.HasIndex(sr => new { sr.SurveyId, sr.UserId })
                      .IsUnique()
                      .HasFilter("[UserId] IS NOT NULL");

                entity.HasIndex(sr => sr.SubmittedAt);

                entity.HasOne(sr => sr.Survey)
                      .WithMany(s => s.Responses)
                      .HasForeignKey(sr => sr.SurveyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Answer ──
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.HasQueryFilter(a => SurveyResponses.Any(sr => sr.Id == a.ResponseId));

                entity.HasIndex(a => a.ResponseId);
                entity.HasIndex(a => a.QuestionId);

                entity.HasOne(a => a.Response)
                      .WithMany(r => r.Answers)
                      .HasForeignKey(a => a.ResponseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Question)
                      .WithMany(q => q.Answers)
                      .HasForeignKey(a => a.QuestionId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(a => a.SelectedOption)
                      .WithMany()
                      .HasForeignKey(a => a.SelectedOptionId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // ── BankQuestion ──
            modelBuilder.Entity<BankQuestion>(entity =>
            {
                entity.Property(bq => bq.Type).HasConversion<int>();
            });
        }
    }
}
