using System;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>
    /// When an account is deactivated, cancels its open tasks.
    /// Fires from an Update step (statecode in the Target) or from a SetState /
    /// SetStateDynamicEntity step, depending on how the record was deactivated.
    /// </summary>
    public class DeactivationCascadeFeature : IPluginFeature
    {
        private const int MaxTasksPerRun = 200; // keeps a synchronous step well inside the 2-minute limit

        public string Name => "Deactivation cascade";

        public bool CanRun(PluginContext ctx)
        {
            if (ctx.Is("Update", PluginStage.PostOperation))
            {
                var target = ctx.Target;
                return target != null && IsInactive(target.GetAttributeValue<OptionSetValue>("statecode"));
            }

            if (ctx.Is("SetState", PluginStage.PostOperation) || ctx.Is("SetStateDynamicEntity", PluginStage.PostOperation))
            {
                var state = ctx.Execution.InputParameters.Contains("State")
                    ? ctx.Execution.InputParameters["State"] as OptionSetValue
                    : null;
                return IsInactive(state);
            }

            return false;
        }

        public void Execute(PluginContext ctx)
        {
            var query = new QueryExpression("task") { ColumnSet = new ColumnSet(false) };
            query.Criteria.AddCondition("regardingobjectid", ConditionOperator.Equal, ctx.PrimaryEntityId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0); // open

            var tasks = ctx.SystemService.RetrieveAll(query);
            var count = Math.Min(tasks.Count, MaxTasksPerRun);

            for (var i = 0; i < count; i++)
            {
                ctx.SystemService.Execute(new SetStateRequest
                {
                    EntityMoniker = tasks[i].ToEntityReference(),
                    State = new OptionSetValue(2),   // Canceled
                    Status = new OptionSetValue(6)   // Canceled
                });
            }

            ctx.Trace("Canceled {0} of {1} open task(s)", count, tasks.Count);
        }

        private static bool IsInactive(OptionSetValue state) => state != null && state.Value == 1;
    }
}
