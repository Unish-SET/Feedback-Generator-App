using FeedBackApp.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace FeedBackApp.Helpers
{
    public static class RoleHelper
    {
        public static bool IsAdmin(string role) =>
            string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase);
    }

    public static class DbExceptionHelper
    {
       
        private static readonly HashSet<int> UniqueConstraintErrors = new() { 2601, 2627 };

        public static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            var inner = ex.InnerException;
            while (inner != null)
            {
                if (inner is SqlException sqlEx &&
                    sqlEx.Errors.Cast<SqlError>().Any(e => UniqueConstraintErrors.Contains(e.Number)))
                    return true;
                inner = inner.InnerException;
            }
            return false;
        }
    }
}
