using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MyCompany.Plugins.Core
{
    public static class Extensions
    {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        public static string GetTrimmed(this Entity entity, string attribute)
        {
            var value = entity.GetAttributeValue<string>(attribute);
            return value == null ? null : value.Trim();
        }

        /// <summary>Returns true if the query matches at least one row.</summary>
        public static bool Exists(this IOrganizationService service, QueryExpression query)
        {
            query.TopCount = 1;
            return service.RetrieveMultiple(query).Entities.Count > 0;
        }

        /// <summary>Retrieves every page of a query.</summary>
        public static List<Entity> RetrieveAll(this IOrganizationService service, QueryExpression query)
        {
            var results = new List<Entity>();
            query.PageInfo = new PagingInfo { PageNumber = 1, Count = 500 };

            while (true)
            {
                var page = service.RetrieveMultiple(query);
                results.AddRange(page.Entities);
                if (!page.MoreRecords) break;

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
            return results;
        }
    }
}
