using System;
using Microsoft.Xrm.Sdk;
using MyCompany.Plugins.Core;
using MyCompany.Plugins.Features;

namespace MyCompany.Plugins
{
    /// <summary>
    /// Single plugin type registered on several steps of the Account table.
    /// It dispatches to feature classes based on message and stage, so every
    /// feature is isolated, testable and easy to add or remove.
    /// See README.md for the step registration table.
    /// </summary>
    public class AccountPlugin : IPlugin
    {
        private const int MaxDepth = 3;

        // Order matters inside the same stage (e.g. normalise before generating a number).
        private static readonly IPluginFeature[] Features =
        {
            new AccountValidationFeature(),    // Create/Update  PreValidation
            new DuplicateAccountFeature(),     // Create/Update  PreValidation
            new NormalizationFeature(),        // Create/Update  PreOperation
            new AccountNumberFeature(),        // Create         PreOperation
            new FollowUpTaskFeature(),         // Create         PostOperation
            new ChangeAuditFeature(),          // Update         PostOperation (needs PreImage)
            new DeactivationCascadeFeature(),  // Update/SetState PostOperation
            new DeleteGuardFeature()           // Delete         PreValidation
        };

        public void Execute(IServiceProvider serviceProvider)
        {
            var ctx = new PluginContext(serviceProvider);

            ctx.Trace("AccountPlugin: {0} / stage {1} / depth {2} / user {3}",
                ctx.MessageName, (int)ctx.Stage, ctx.Depth, ctx.UserId);

            if (ctx.Depth > MaxDepth)
            {
                ctx.Trace("Max depth exceeded - exiting to avoid an infinite loop.");
                return;
            }

            if (!string.Equals(ctx.EntityName, "account", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var feature in Features)
            {
                if (!feature.CanRun(ctx)) continue;

                ctx.Trace("[{0}] running", feature.Name);
                try
                {
                    feature.Execute(ctx);
                }
                catch (InvalidPluginExecutionException)
                {
                    throw; // business-rule message meant for the user - keep as is
                }
                catch (Exception ex)
                {
                    ctx.Trace("[{0}] failed: {1}", feature.Name, ex);
                    throw new InvalidPluginExecutionException(
                        string.Format("{0} failed: {1}", feature.Name, ex.Message), ex);
                }
            }
        }
    }
}
