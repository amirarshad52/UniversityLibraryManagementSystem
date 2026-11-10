using System.Data.Entity;

namespace ULMS.Web.Data
{
    public class ULMSDbContext : DbContext
    {
        public ULMSDbContext(string connectionString)
            : base(connectionString)
        {
        }
    }
}