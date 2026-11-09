using System;
using Microsoft.Xrm.Sdk;

namespace MyCompany.Plugins.Core
{
    /// <summary>Thin wrapper around the service provider so features stay short and readable.</summary>
    public sealed class PluginContext
    {
        private readonly IOrganizationServiceFactory _factory;
        private IOrganizationService _userService;
        private IOrganizationService _systemService;

        public PluginContext(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            Tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            Execution = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            _factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
        }

        public ITracingService Tracing { get; }
        public IPluginExecutionContext Execution { get; }

        public string MessageName => Execution.MessageName;
        public PluginStage Stage => (PluginStage)Execution.Stage;
        public string EntityName => Execution.PrimaryEntityName;
        public Guid PrimaryEntityId => Execution.PrimaryEntityId;
        public int Depth => Execution.Depth;
        public Guid UserId => Execution.UserId;
        public Guid InitiatingUserId => Execution.InitiatingUserId;

        /// <summary>Runs with the permissions of the user executing the operation.</summary>
        public IOrganizationService UserService =>
            _userService ?? (_userService = _factory.CreateOrganizationService(Execution.UserId));

        /// <summary>Runs as SYSTEM. Use for housekeeping that the end user may not have privileges for.</summary>
        public IOrganizationService SystemService =>
            _systemService ?? (_systemService = _factory.CreateOrganizationService(null));

        /// <summary>Target entity for Create/Update. Null for Delete.</summary>
        public Entity Target =>
            Execution.InputParameters.Contains("Target") ? Execution.InputParameters["Target"] as Entity : null;

        /// <summary>Pre-image registered with the alias "PreImage" (Update/Delete only).</summary>
        public Entity PreImage =>
            Execution.PreEntityImages != null && Execution.PreEntityImages.Contains("PreImage")
                ? Execution.PreEntityImages["PreImage"]
                : null;

        public bool Is(string message, PluginStage stage) =>
            string.Equals(MessageName, message, StringComparison.OrdinalIgnoreCase) && Stage == stage;

        public void Trace(string format, params object[] args)
        {
            if (args == null || args.Length == 0) Tracing.Trace("{0}", format);
            else Tracing.Trace(format, args);
        }
    }
}
