using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using MySql.Data.EntityFramework;

namespace ULMS.Web.Data
{
    public class ULMSMySqlEFConfiguration : DbConfiguration
    {
        public ULMSMySqlEFConfiguration()
        {
            AddDependencyResolver(new MySqlDependencyResolver());

            SetManifestTokenResolver(
                new MariaDbManifestTokenResolver()
            );
        }
    }

    internal class MariaDbManifestTokenResolver : IManifestTokenResolver
    {
        public string ResolveManifestToken(DbConnection connection)
        {
            return "5.7";
        }
    }
}
