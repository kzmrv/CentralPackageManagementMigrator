using System.Diagnostics;

var targetProject = "";// input
ProcessStartInfo processStartInfo = new ProcessStartInfo();

processStartInfo.FileName = Path.GetFullPath($"{Directory.GetCurrentDirectory()}\\..\\..\\..\\..\\CpmMigrator\\bin\\Debug\\net7.0\\CpmMigrator.exe");
processStartInfo.Arguments = $"{targetProject} -v Debug";
Process.Start(processStartInfo)!.WaitForExit();
