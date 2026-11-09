using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>Generates ACC-yyyyMMdd-XXXXXX when no account number is supplied.</summary>
    public class AccountNumberFeature : IPluginFeature
    {
        private const int MaxAttempts = 5;

        public string Name => "Account number generator";

        public bool CanRun(PluginContext ctx) => ctx.Is("Create", PluginStage.PreOperation);

        public void Execute(PluginContext ctx)
        {
            var target = ctx.Target;
            if (target == null) return;
            if (!target.GetAttributeValue<string>("accountnumber").IsBlank()) return;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var candidate = "ACC-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" +
                                Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();

                var query = new QueryExpression("account") { ColumnSet = new ColumnSet(false) };
                query.Criteria.AddCondition("accountnumber", ConditionOperator.Equal, candidate);

                if (!ctx.SystemService.Exists(query))
                {
                    target["accountnumber"] = candidate;
                    ctx.Trace("Generated account number {0}", candidate);
                    return;
                }
            }

            throw new InvalidPluginExecutionException("Could not generate a unique account number. Please try again.");
        }
    }
}
