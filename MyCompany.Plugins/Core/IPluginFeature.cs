namespace MyCompany.Plugins.Core
{
    /// <summary>Pipeline stages as registered in the Plugin Registration Tool.</summary>
    public enum PluginStage
    {
        PreValidation = 10,
        PreOperation = 20,
        PostOperation = 40
    }

    /// <summary>
    /// One self-contained piece of business logic. Features MUST be stateless
    /// because Dataverse caches plugin instances and reuses them across requests.
    /// </summary>
    public interface IPluginFeature
    {
        string Name { get; }

        /// <summary>Return true when this feature should run for the current message/stage.</summary>
        bool CanRun(PluginContext ctx);

        void Execute(PluginContext ctx);
    }
}
