using System;
using System.Diagnostics;
using System.IO;
#if NETFRAMEWORK
using System.Reflection;
#endif

using System.Threading.Tasks;

namespace ConsoleApp1
{
    public static class Program
    {
        private static readonly FileInfo MSBuildExePath = new(@"C:\Program Files\Visual Studio 2022 Preview\MSBuild\Current\Bin\MSBuild.exe"); // @"D:\msbuild\artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\amd64\MSBuild.exe"

        public static async Task<int> Main(string[] args)
        {
            int result = 0;

#if NETFRAMEWORK
            if (AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", MSBuildExePath.FullName);
                Environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
                Environment.SetEnvironmentVariable("MSBuildCacheFileEnumerations", "1");
                Environment.SetEnvironmentVariable("MsBuildCacheFileExistence", "1");
                Environment.SetEnvironmentVariable("MSBuildLoadAllFilesAsReadonly", "1");
                Environment.SetEnvironmentVariable("MSBuildSkipEagerWildCardEvaluationRegexes", @"[*?]+.*(?<!proj)$");
                Environment.SetEnvironmentVariable("MSBUILDTRUNCATETASKINPUTS", "1");
                Environment.SetEnvironmentVariable("MsBuildUseSimpleProjectRootElementCacheConcurrency", "1");

                AppDomainSetup appDomainSetup = new()
                {
                    ApplicationBase = MSBuildExePath.DirectoryName,
                    ConfigurationFile = Path.Combine(MSBuildExePath.DirectoryName, "MSBuild.exe.config"),
                };

                AppDomain appDomain = AppDomain.CreateDomain("MSBuildExeConfig", securityInfo: null, appDomainSetup);

                result = await Task.Run(() => appDomain.ExecuteAssemblyByName(Assembly.GetExecutingAssembly().GetName(), args));

                return result;
            }
#endif

            Debugger.Launch();

            SimpleGraph graph = await SimpleGraph.CreateAsync(["A"], SimpleGraph._rawGraph);

            //string projectFullPath = args.FirstOrDefault() ?? @"D:\Repros\CentralPackageVersionFactory\ClassLibrary1.Tests\ClassLibrary1.Tests.csproj";

            //Console.WriteLine("ProjectFullPath={0}", projectFullPath);

            //MSBuildGraph p = new();

            //p.Go(projectFullPath);

            return result;
        }
    }
}