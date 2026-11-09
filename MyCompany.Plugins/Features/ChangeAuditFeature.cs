using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>
    /// Writes a Note on the account listing which monitored fields changed (old -> new).
    /// Requires a pre-image with alias "PreImage" containing the monitored attributes.
    /// </summary>
    public class ChangeAuditFeature : IPluginFeature
    {
        private static readonly Dictionary<string, string> Monitored = new Dictionary<string, string>
        {
            { "name", "Account Name" },
            { "telephone1", "Main Phone" },
            { "emailaddress1", "Email" },
            { "websiteurl", "Website" },
            { "creditlimit", "Credit Limit" },
            { "revenue", "Annual Revenue" },
            { "primarycontactid", "Primary Contact" },
            { "ownerid", "Owner" },
            { "statecode", "Status" }
        };

        public string Name => "Change audit note";

        public bool CanRun(PluginContext ctx) => ctx.Is("Update", PluginStage.PostOperation);

        public void Execute(PluginContext ctx)
        {
            var target = ctx.Target;
            var pre = ctx.PreImage;
            if (target == null) return;
            if (pre == null)
            {
                ctx.Trace("PreImage not registered - audit skipped.");
                return;
            }

            var lines = new List<string>();
            foreach (var item in Monitored)
            {
                if (!target.Contains(item.Key)) continue;

                var before = pre.Contains(item.Key) ? pre[item.Key] : null;
                var after = target[item.Key];
                if (AreEqual(before, after)) continue;

                lines.Add(string.Format("{0}: {1} -> {2}",
                    item.Value, Display(item.Key, before), Display(item.Key, after)));
            }

            if (lines.Count == 0) return;

            var note = new Entity("annotation");
            note["subject"] = "Audit: account fields changed";
            note["notetext"] = string.Format("Changed by {0} on {1:yyyy-MM-dd HH:mm} UTC\n{2}",
                GetUserName(ctx), DateTime.UtcNow, string.Join("\n", lines));
            note["objectid"] = new EntityReference("account", ctx.PrimaryEntityId);

            ctx.SystemService.Create(note);
            ctx.Trace("Audit note written with {0} change(s)", lines.Count);
        }

        private static object Clean(object value)
        {
            var text = value as string;
            return text != null && text.Trim().Length == 0 ? null : value;
        }

        private static bool AreEqual(object a, object b)
        {
            a = Clean(a);
            b = Clean(b);
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            var refA = a as EntityReference;
            var refB = b as EntityReference;
            if (refA != null && refB != null) return refA.Id == refB.Id;

            var moneyA = a as Money;
            var moneyB = b as Money;
            if (moneyA != null && moneyB != null) return moneyA.Value == moneyB.Value;

            var optA = a as OptionSetValue;
            var optB = b as OptionSetValue;
            if (optA != null && optB != null) return optA.Value == optB.Value;

            return a.Equals(b);
        }

        private static string Display(string attribute, object value)
        {
            value = Clean(value);
            if (value == null) return "(empty)";

            var money = value as Money;
            if (money != null) return money.Value.ToString("N2", CultureInfo.InvariantCulture);

            var reference = value as EntityReference;
            if (reference != null)
                return string.IsNullOrEmpty(reference.Name) ? reference.LogicalName + ":" + reference.Id : reference.Name;

            var option = value as OptionSetValue;
            if (option != null)
            {
                if (attribute == "statecode") return option.Value == 0 ? "Active" : "Inactive";
                return option.Value.ToString(CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string GetUserName(PluginContext ctx)
        {
            try
            {
                var user = ctx.SystemService.Retrieve("systemuser", ctx.InitiatingUserId, new ColumnSet("fullname"));
                return user.GetAttributeValue<string>("fullname") ?? ctx.InitiatingUserId.ToString();
            }
            catch (Exception)
            {
                return ctx.InitiatingUserId.ToString();
            }
        }
    }
}
