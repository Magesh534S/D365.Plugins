using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>Blocks creating/renaming an account to a name that already exists on an active account.</summary>
    public class DuplicateAccountFeature : IPluginFeature
    {
        public string Name => "Duplicate account check";

        public bool CanRun(PluginContext ctx)
        {
            if (ctx.Is("Create", PluginStage.PreValidation)) return true;
            return ctx.Is("Update", PluginStage.PreValidation) && ctx.Target != null && ctx.Target.Contains("name");
        }

        public void Execute(PluginContext ctx)
        {
            var name = ctx.Target.GetTrimmed("name");
            if (name.IsBlank()) return; // the validation feature reports this

            var query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("accountnumber"),
                TopCount = 1
            };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

            if (string.Equals(ctx.MessageName, "Update", StringComparison.OrdinalIgnoreCase))
            {
                query.Criteria.AddCondition("accountid", ConditionOperator.NotEqual, ctx.PrimaryEntityId);
            }

            // System service so the check sees records the user cannot read.
            var duplicate = ctx.SystemService.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (duplicate != null)
            {
                var number = duplicate.GetAttributeValue<string>("accountnumber");
                throw new InvalidPluginExecutionException(
                    string.Format("An active account named '{0}' already exists{1}.",
                        name, number.IsBlank() ? string.Empty : " (" + number + ")"));
            }
        }
    }
}
