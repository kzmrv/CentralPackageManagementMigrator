using System.Text.RegularExpressions;
using System.Xml;

using Buildalyzer;

using Serilog;
using Serilog.Events;

namespace CpmMigrator
{
    public class ProjectFileParser
    {
        public static Dictionary<string, NuGetPackageInfo>? ProcessProjectFile(MigratorCommand.Settings settings, FileInfo projectFile, Dictionary<string, NuGetPackageInfo> latestPackage)
        {
            Log.Logger.Information("Reading project {FullName} for PackageReferences", projectFile.FullName);
            var projectDocument = new XmlDocument();
            using var fileStream = projectFile.OpenRead();
            projectDocument.Load(fileStream);
            Log.Logger.Debug("Project XML loaded");

            var packagesInProject = ParseProjectFile(settings, projectFile.FullName, projectDocument);

            foreach (var (packageName, packageInfo) in packagesInProject)
            {
                var packageVersion = packageInfo.Version;
                if (latestPackage.ContainsKey(packageName))
                {
                    Log.Logger.Debug("Latest package lookup contains package {PackageName}", packageName);
                    var latestPackageVersion = latestPackage[packageName];
                    if (packageInfo.CompareTo(latestPackageVersion) == 1)
                    {
                        // package has a higher version then latestPackageVersion
                        latestPackage[packageName] = packageInfo;
                        Log.Logger.Debug("Updated latest package version for {PackageName} to version {PackageVersion}", packageName, packageVersion);
                    }
                    else
                    {
                        Log.Logger.Debug("Package had version equal to or less than latest version");
                    }
                }
                else
                {
                    latestPackage.Add(packageName, packageInfo);
                    Log.Logger.Debug("Added new package {PackageName} as latest package with version {PackageVersion}", packageName, packageVersion);
                }
            }

            return packagesInProject.Any() ? packagesInProject : null;
        }

        public static Dictionary<string, NuGetPackageInfo> ParseProjectFile(MigratorCommand.Settings settings, string fileFullName, XmlDocument projectDocument)
        {
            Log.Logger.Debug("Checking for legacy XML namespace");
            var msbuildXmlQueryHelper = new MSBuildXmlNamespaceQueryHelper(projectDocument);
            var legacyNamespace = msbuildXmlQueryHelper.SelectSingleNode(projectDocument, "Project", true) != null;
            Log.Logger.Debug("Legacy namespace used {LegacyNamespace}", legacyNamespace);

            Log.Logger.Debug("Checking for PackageReferences");
            var packageReferences = msbuildXmlQueryHelper.SelectNodes(projectDocument, "//PackageReference", legacyNamespace);
            Log.Logger.Information("Found {Count} PackageReferences", packageReferences?.Count ?? 0);

            IAnalyzerResult? firstBuildResult = null;

            var packagesInProject = new Dictionary<string, NuGetPackageInfo>();
            if (packageReferences == null || packageReferences.Count < 1)
            {
                Log.Logger.Debug("No PackageReferences found in {FullName}", fileFullName);
                return packagesInProject;
            }

            foreach (XmlNode packageReference in packageReferences)
            {
                Log.Logger.Debug("Checking Include and Version attributes on PackageReference");
                var packageName = packageReference.Attributes?["Include"]?.Value;
                var packageVersion = packageReference.Attributes?["Version"]?.Value;
                if (string.IsNullOrEmpty(packageVersion))
                {
                    Log.Logger.Warning(
                        "Project {FullName} contained PackageReferences without Version attributes, Central Package Versioning SDK or Central Package Management could be setup",
                        fileFullName);
                    continue;
                }

                if (packageName == null)
                {
                    Log.Logger.Warning("Null package name detected: {PackageReference}", packageReference);
                    continue;
                }

                var variablesToReplace = Regex.Matches(packageVersion, @"\$\(.*\)");
                foreach (Match replaceVariable in variablesToReplace)
                {
                    if (firstBuildResult == null)
                    {
                        try
                        {
                            var buildAnalyzerLogger = CreateLoggerForBuildAnalyzer(settings);
                            var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(buildAnalyzerLogger, false);

                            firstBuildResult = DesignTimeBuildHelper.ExecuteDesignTimeBuild(loggerFactory, fileFullName);
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Error(ex, "Failed to execute design-time build on: {FullName}", fileFullName);
                            throw;
                        }
                    }

                    Log.Logger.Debug("\tResolving MSBuild property: {Value}", replaceVariable.Value);
                    var propertyName = replaceVariable.Value.Substring(2, replaceVariable.Value.Length - 3); // Skip first 2 chars, extract length minus first 2 chars and last char = 3
                    var resolvedVariable = firstBuildResult.GetProperty(propertyName);
                    Log.Logger.Debug("\tResolved {PropertyName} to: {ResolvedVariable}", propertyName, resolvedVariable);
                    packageVersion = packageVersion.Replace(replaceVariable.Value, resolvedVariable);
                }

                var packageInfo = new NuGetPackageInfo(packageName, packageVersion);
                Log.Logger.Debug("Nuget package found in {FullName}, package ID {PackageName} and version {PackageVersion}", fileFullName, packageName, packageVersion);

                packagesInProject.Add(packageName, packageInfo);
                Log.Logger.Debug("Package added for lookup inside project");
            }

            return packagesInProject;

            Serilog.Core.Logger CreateLoggerForBuildAnalyzer(LogSettings logSettings)
            {
                Log.Logger.Debug("Configuring logger for build analyzer");
                var outputTemplate = $"[DesignTimeBuild tid:{{ThreadId}} {{Level:u3}}] {{Message:lj}}{{NewLine}}{{Exception}}";

                var logLevel = logSettings.LogVerbosity switch
                {
                    LogLevel.Debug => LogEventLevel.Debug,
                    LogLevel.Warning => LogEventLevel.Warning,
                    LogLevel.Info => LogEventLevel.Information,
                    _ => throw new NotImplementedException($"{nameof(LogSettings.LogVerbosity)} of value {logSettings.LogVerbosity} not implemented for setting logger for BuildAnalyzer"),
                };

                var logConfig = new LoggerConfiguration()
                    .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                    .Enrich.WithProcessId()
                    .Enrich.WithProcessName()
                    .Enrich.WithThreadId()
                    .WriteTo.Console(
                        outputTemplate: outputTemplate,
                        restrictedToMinimumLevel: logLevel);

                if (!string.IsNullOrEmpty(logSettings.LogFile))
                {
                    logConfig = logConfig.WriteTo.File(logSettings.LogFile, outputTemplate: outputTemplate, shared: true, rollOnFileSizeLimit: true);
                }

                return logConfig.CreateLogger();
            }
        }
    }
}
