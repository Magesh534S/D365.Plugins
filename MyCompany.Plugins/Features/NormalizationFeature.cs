using System;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>Cleans data before it is saved (runs in PreOperation, so it edits the Target directly).</summary>
    public class NormalizationFeature : IPluginFeature
    {
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);

        public string Name => "Data normalisation";

        public bool CanRun(PluginContext ctx) =>
            ctx.Is("Create", PluginStage.PreOperation) || ctx.Is("Update", PluginStage.PreOperation);

        public void Execute(PluginContext ctx)
        {
            var target = ctx.Target;
            if (target == null) return;

            Normalize(target, "name", v => Spaces.Replace(v, " "));
            Normalize(target, "emailaddress1", v => v.ToLowerInvariant());
            Normalize(target, "telephone1", v => Spaces.Replace(v, " "));
            Normalize(target, "websiteurl", v => v.Contains("://") ? v : "https://" + v);
            Normalize(target, "accountnumber", v => v.ToUpperInvariant());
        }

        private static void Normalize(Entity target, string attribute, Func<string, string> transform)
        {
            var value = target.GetAttributeValue<string>(attribute);
            if (value == null) return;

            var trimmed = value.Trim();
            target[attribute] = trimmed.Length == 0 ? null : transform(trimmed);
        }
    }
}
