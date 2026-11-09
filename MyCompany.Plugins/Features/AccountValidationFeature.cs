using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using MyCompany.Plugins.Core;

namespace MyCompany.Plugins.Features
{
    /// <summary>Validates required and formatted fields and reports ALL problems in one message.</summary>
    public class AccountValidationFeature : IPluginFeature
    {
        private static readonly Regex EmailRegex =
            new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => "Account validation";

        public bool CanRun(PluginContext ctx) =>
            ctx.Is("Create", PluginStage.PreValidation) || ctx.Is("Update", PluginStage.PreValidation);

        public void Execute(PluginContext ctx)
        {
            var target = ctx.Target;
            if (target == null) return;

            var isCreate = string.Equals(ctx.MessageName, "Create", StringComparison.OrdinalIgnoreCase);
            var errors = new List<string>();

            if (isCreate || target.Contains("name"))
            {
                var name = target.GetTrimmed("name");
                if (name.IsBlank()) errors.Add("Account name is required.");
                else if (name.Length > 160) errors.Add("Account name cannot exceed 160 characters.");
            }

            if (target.Contains("emailaddress1"))
            {
                var email = target.GetTrimmed("emailaddress1");
                if (!email.IsBlank() && !EmailRegex.IsMatch(email))
                    errors.Add("Email address is not in a valid format.");
            }

            if (target.Contains("telephone1"))
            {
                var phone = target.GetTrimmed("telephone1");
                if (!phone.IsBlank())
                {
                    var digits = phone.Count(char.IsDigit);
                    if (digits < 7 || digits > 15)
                        errors.Add("Main phone must contain between 7 and 15 digits.");
                }
            }

            if (target.Contains("websiteurl"))
            {
                var site = target.GetTrimmed("websiteurl");
                if (!site.IsBlank())
                {
                    var candidate = site.Contains("://") ? site : "https://" + site;
                    Uri uri;
                    var ok = Uri.TryCreate(candidate, UriKind.Absolute, out uri)
                             && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                             && uri.Host.Contains(".");
                    if (!ok) errors.Add("Website is not a valid URL.");
                }
            }

            var creditLimit = target.GetAttributeValue<Money>("creditlimit");
            if (creditLimit != null && creditLimit.Value < 0) errors.Add("Credit limit cannot be negative.");

            var revenue = target.GetAttributeValue<Money>("revenue");
            if (revenue != null && revenue.Value < 0) errors.Add("Annual revenue cannot be negative.");

            if (target.Contains("numberofemployees"))
            {
                var employees = target.GetAttributeValue<int?>("numberofemployees");
                if (employees.HasValue && employees.Value < 0) errors.Add("Number of employees cannot be negative.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidPluginExecutionException(
                    "Account validation failed:\n- " + string.Join("\n- ", errors));
            }
        }
    }
}
