using System;
using Microsoft.Xrm.Sdk;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>Creates a follow-up task for the account owner after an account is created.</summary>
    public class FollowUpTaskFeature : IPluginFeature
    {
        private const int FollowUpDays = 3;

        public string Name => "Follow-up task";

        public bool CanRun(PluginContext ctx) => ctx.Is("Create", PluginStage.PostOperation);

        public void Execute(PluginContext ctx)
        {
            var target = ctx.Target;
            if (target == null) return;

            var name = target.GetAttributeValue<string>("name") ?? "(unnamed)";
            var owner = target.GetAttributeValue<EntityReference>("ownerid")
                        ?? new EntityReference("systemuser", ctx.UserId);

            var task = new Entity("task");
            task["subject"] = "Follow up with new account: " + name;
            task["description"] = "Auto-created when the account was created. Introduce yourself and confirm the contact details.";
            task["scheduledend"] = DateTime.UtcNow.AddDays(FollowUpDays);
            task["prioritycode"] = new OptionSetValue(1); // Normal
            task["regardingobjectid"] = new EntityReference("account", ctx.PrimaryEntityId);
            task["ownerid"] = owner;

            // System service: the creating user may lack Task create privilege.
            var taskId = ctx.SystemService.Create(task);
            ctx.Trace("Follow-up task {0} created", taskId);
        }
    }
}
