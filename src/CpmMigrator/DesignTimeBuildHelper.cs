using Buildalyzer;
using Buildalyzer.Environment;

using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

using Serilog;

namespace CpmMigrator
{
    public static class DesignTimeBuildHelper
    {
        public static IAnalyzerResult ExecuteDesignTimeBuild(ILoggerFactory loggerFactory, string fileFullName)
        {
            var options = new AnalyzerManagerOptions
            {
                LoggerFactory = loggerFactory,
            };

            Log.Logger.Debug("Setting up AnalyzerManager");
            var manager = new AnalyzerManager(options);

            Log.Logger.Information("Getting project from analyzer manager");
            var project = manager.GetProject(fileFullName);
            Log.Logger.Information("Evaluating project in design-time build");
            var environmentOptions = new EnvironmentOptions()
            {
                Restore = false,
            };

            var msbuildPath = TryGetMsbuildPathFromEnvironment();
            environmentOptions.EnvironmentVariables.Add("MSBUILD_EXE_PATH", Path.Combine(msbuildPath));

            var designTimeBuildResult = project.Build(environmentOptions);
            Log.Logger.Information("Build result received, success = {OverallSuccess}", designTimeBuildResult.OverallSuccess);

            if (project.ProjectFile.IsMultiTargeted || designTimeBuildResult.Results.Count() > 1)
            {
                Log.Logger.Warning("Project was multi-targeted = {IsMultiTargeted} or result count was greater than 1, only first result used for discovering properties", project.ProjectFile.IsMultiTargeted);
            }

            Log.Logger.Debug("Getting first result from design-time build");
            return designTimeBuildResult.First();
        }

        private static string TryGetMsbuildPathFromEnvironment()
        {
            var msbuildInstances = MSBuildLocator.QueryVisualStudioInstances();

            return msbuildInstances.First().MSBuildPath;
        }
    }
}
