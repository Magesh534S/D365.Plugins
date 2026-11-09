using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>Prevents deleting an account that still has open opportunities or active cases.</summary>
    public class DeleteGuardFeature : IPluginFeature
    {
        public string Name => "Delete guard";

        public bool CanRun(PluginContext ctx) => ctx.Is("Delete", PluginStage.PreValidation);

        public void Execute(PluginContext ctx)
        {
            var blockers = new List<string>();

            if (HasOpenRecords(ctx, "opportunity", "parentaccountid")) blockers.Add("open opportunities");
            if (HasOpenRecords(ctx, "incident", "customerid")) blockers.Add("active cases");

            if (blockers.Count > 0)
            {
                throw new InvalidPluginExecutionException(
                    "This account cannot be deleted because it still has " +
                    string.Join(" and ", blockers) + ". Close or reassign them first.");
            }
        }

        private static bool HasOpenRecords(PluginContext ctx, string entity, string accountLookup)
        {
            var query = new QueryExpression(entity) { ColumnSet = new ColumnSet(false) };
            query.Criteria.AddCondition(accountLookup, ConditionOperator.Equal, ctx.PrimaryEntityId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            return ctx.SystemService.Exists(query);
        }
    }
}
