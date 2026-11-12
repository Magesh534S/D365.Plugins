using System;
using Microsoft.Xrm.Sdk;

namespace MyCompany.Plugins.Features
{
    public class AccountSourceTagPlugin : IPlugin
    {
        private const string TagKey = "tag";
        private const string SourceKey = "MyCompany.Source";
        private const string SkipKey = "MyCompany.SkipFollowUp";
        private const string ImportSequenceKey = "importsequencenumber";

        public void Execute(IServiceProvider serviceProvider)
        {
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context.PrimaryEntityName != "account" || context.MessageName != "Create")
                return;
            if (context.Depth > 3)
                return;

            switch (context.Stage)
            {
                case 20:
                    SetContextFromTag(context, trace);
                    break;

                case 40:
                    ActOnContext(serviceProvider, context, trace);
                    break;
            }
        }

        // ---------- Plugin A: read the Tag, publish decisions ----------
        private static void SetContextFromTag(IPluginExecutionContext context, ITracingService trace)
        {
            // Temporary diagnostics: remove once detection works
            trace.Trace("Stage={0}, Mode={1}, Depth={2}, InitiatingUser={3}, User={4}",
                context.Stage, context.Mode, context.Depth, context.InitiatingUserId, context.UserId);

            var target = context.InputParameters.Contains("Target")
                ? context.InputParameters["Target"] as Entity
                : null;
            if (target != null)
            {
                foreach (var a in target.Attributes)
                    trace.Trace("Target attr: {0}", a.Key);
            }
            foreach (var kv in context.SharedVariables)
                trace.Trace("SharedVariable: {0} = {1}", kv.Key, kv.Value);
            trace.Trace("HasParent = {0}", context.ParentContext != null);

            string tag;
            if (!TryGetShared(context, TagKey, out tag))
            {
                tag = IsImport(context) ? "Import" : "Direct";
            }

            trace.Trace("Tag received: {0}", tag);

            context.SharedVariables[SourceKey] = tag;

            if (tag.Equals("Migration", StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("Batch", StringComparison.OrdinalIgnoreCase))
            {
                context.SharedVariables[SkipKey] = true;
                trace.Trace("Follow-up note will be skipped for tag {0}", tag);
            }
        }

        // ---------- Plugin B: read what A published, act on it ----------
        private static void ActOnContext(IServiceProvider serviceProvider, IPluginExecutionContext context, ITracingService trace)
        {
            string source;
            if (!TryGetShared(context, SourceKey, out source))
            {
                trace.Trace("No source recorded; PreOperation step may not be registered.");
                return;
            }

            string skip;
            if (TryGetShared(context, SkipKey, out skip))
            {
                trace.Trace("Skipping note. Source = {0}", source);
                return;
            }

            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            var note = new Entity("annotation");
            note["subject"] = "Account created via " + source;
            note["notetext"] = "Source tag: " + source;
            note["objectid"] = new EntityReference("account", context.PrimaryEntityId);
            note["objecttypecode"] = "account";
            service.Create(note);

            trace.Trace("Note created for source {0}", source);

            var update = new Entity("account", context.PrimaryEntityId);
            update["description"] = "AccountSourceTagPlugin ran. Source: " + source;
            service.Update(update);

            trace.Trace("Description updated for source {0}", source);
        }

        private static bool IsImport(IPluginExecutionContext context)
        {
            if (!context.InputParameters.Contains("Target"))
                return false;

            var target = context.InputParameters["Target"] as Entity;

            return target != null
                && target.Contains(ImportSequenceKey)
                && target[ImportSequenceKey] != null;
        }

        private static bool TryGetShared(IPluginExecutionContext context, string key, out string value)
        {
            for (var c = context; c != null; c = c.ParentContext)
            {
                if (c.SharedVariables.ContainsKey(key) && c.SharedVariables[key] != null)
                {
                    value = c.SharedVariables[key].ToString();
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}