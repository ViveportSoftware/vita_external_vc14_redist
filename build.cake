#addin "nuget:?package=Cake.Git&version=0.16.1"
#addin "nuget:?package=Cake.FileHelpers&version=2.0.0"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var configuration = Argument("configuration", "Debug");
var revision = EnvironmentVariable("BUILD_NUMBER") ?? Argument("revision", "9999");
var target = Argument("target", "Default");


//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define git commit id
var commitId = "SNAPSHOT";

// Define product name and version
var product = "Htc.Vita.External.VC14.Redist";
var companyName = "HTC";
var version = "14.0.24215";
var semanticVersion = string.Format("{0}.{1}", version, revision);
var ciVersion = string.Format("{0}.{1}", version, "0");
var nugetTags = new [] {"htc", "vita", "external", "vc14", "redist"};
var projectUrl = "https://github.com/ViveportSoftware/vita_external_vc14_redist/";
var description = "HTC Vita external package: VC14 redistributable";

// Define copyright
var copyright = string.Format("Copyright © 2018 - {0}", DateTime.Now.Year);

// Define timestamp for signing
var lastSignTimestamp = DateTime.Now;
var signIntervalInMilli = 1000 * 5;

// Define path
var targetFileNameX64 = string.Format("vcredist_2015_x64-{0}.exe", version);
var targetFileNameX86 = string.Format("vcredist_2015_x86-{0}.exe", version);
var targetFileUrlX64 = "https://download.microsoft.com/download/6/A/A/6AA4EDFF-645B-48C5-81CC-ED5963AEAD48/vc_redist.x64.exe";
var targetFileUrlX86 = "https://download.microsoft.com/download/6/A/A/6AA4EDFF-645B-48C5-81CC-ED5963AEAD48/vc_redist.x86.exe";
var targetSha512sumFileNameX64 = string.Format("{0}.sha512sum", targetFileNameX64);
var targetSha512sumFileNameX86 = string.Format("{0}.sha512sum", targetFileNameX86);

// Define directories.
var distDir = Directory("./dist");
var tempDir = Directory("./temp");
var nugetDir = distDir + Directory(configuration) + Directory("nuget");
var tempOutputDir = tempDir + Directory(configuration);

// Define nuget push source and key
var nugetApiKey = EnvironmentVariable("NUGET_PUSH_TOKEN") ?? EnvironmentVariable("NUGET_APIKEY") ?? "NOTSET";
var nugetSource = EnvironmentVariable("NUGET_PUSH_PATH") ?? EnvironmentVariable("NUGET_SOURCE") ?? "NOTSET";


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Fetch-Git-Commit-ID")
    .ContinueOnError()
    .Does(() =>
{
    var lastCommit = GitLogTip(MakeAbsolute(Directory(".")));
    commitId = lastCommit.Sha;
});

Task("Display-Config")
    .IsDependentOn("Fetch-Git-Commit-ID")
    .Does(() =>
{
    Information("Build target: {0}", target);
    Information("Build configuration: {0}", configuration);
    Information("Build commitId: {0}", commitId);
    if ("Release".Equals(configuration))
    {
        Information("Build version: {0}", semanticVersion);
    }
    else
    {
        Information("Build version: {0}-CI{1}", ciVersion, revision);
    }
});

Task("Clean-Workspace")
    .IsDependentOn("Display-Config")
    .Does(() =>
{
    CleanDirectory(distDir);
    CleanDirectory(tempDir);
});

Task("Download-Dependent-Binaries")
    .IsDependentOn("Clean-Workspace")
    .Does(() =>
{
    CreateDirectory(tempOutputDir);

    var targetFileX64 = tempOutputDir + File(targetFileNameX64);
    DownloadFile(targetFileUrlX64, targetFileX64);
    FileWriteText(
            tempOutputDir + File(targetSha512sumFileNameX64),
            string.Format(
                    "{0} *{1}",
                    CalculateFileHash(
                            targetFileX64,
                            HashAlgorithm.SHA512
                    ).ToHex(),
                    targetFileNameX64
            )
    );

    var targetFileX86 = tempOutputDir + File(targetFileNameX86);
    DownloadFile(targetFileUrlX86, targetFileX86);
    FileWriteText(
            tempOutputDir + File(targetSha512sumFileNameX86),
            string.Format(
                    "{0} *{1}",
                    CalculateFileHash(
                            targetFileX86,
                            HashAlgorithm.SHA512
                    ).ToHex(),
                    targetFileNameX86
            )
    );
});

Task("Build-NuGet-Package")
    .IsDependentOn("Download-Dependent-Binaries")
    .Does(() =>
{
    CreateDirectory(nugetDir);
    var nugetPackVersion = semanticVersion;
    if (!"Release".Equals(configuration))
    {
        nugetPackVersion = string.Format("{0}-CI{1}", ciVersion, revision);
    }
    Information("Pack version: {0}", nugetPackVersion);
    var nuGetPackSettings = new NuGetPackSettings
    {
            Id = product,
            Version = nugetPackVersion,
            Authors = new[] {"HTC"},
            Description = description + " [CommitId: " + commitId + "]",
            Copyright = copyright,
            ProjectUrl = new Uri(projectUrl),
            Tags = nugetTags,
            RequireLicenseAcceptance= false,
            Files = new []
            {
                    new NuSpecContent
                    {
                            Source = "*.*",
                            Target = "content"
                    }
            },
            Properties = new Dictionary<string, string>
            {
                    {"Configuration", configuration}
            },
            BasePath = tempOutputDir,
            OutputDirectory = nugetDir
    };

    NuGetPack(nuGetPackSettings);
});

Task("Publish-NuGet-Package")
    .WithCriteria(() => "Release".Equals(configuration) && !"NOTSET".Equals(nugetApiKey) && !"NOTSET".Equals(nugetSource))
    .IsDependentOn("Build-NuGet-Package")
    .Does(() =>
{
    var nugetPushVersion = semanticVersion;
    if (!"Release".Equals(configuration))
    {
        nugetPushVersion = string.Format("{0}-CI{1}", ciVersion, revision);
    }
    Information("Publish version: {0}", nugetPushVersion);
    var package = string.Format("./dist/{0}/nuget/{1}.{2}.nupkg", configuration, product, nugetPushVersion);
    NuGetPush(
            package,
            new NuGetPushSettings
            {
                    Source = nugetSource,
                    ApiKey = nugetApiKey
            }
    );
});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build-NuGet-Package");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
