#load "build/blobupload.cake"
#load "build/clickonce.cake"
#load "build/builddata.cake"

var target = Argument(
    "target",
    (
        BuildSystem.GitHubActions.Environment.PullRequest.IsPullRequest
        || !BuildSystem.GitHubActions.IsRunningOnGitHubActions
    )   ? "Default"
        : "Publish-ClickOnce"
    );

if (BuildSystem.GitHubActions.IsRunningOnGitHubActions)
{
    TaskSetup(context=> System.Console.WriteLine($"::group::{context.Task.Name.Quote()}"));
    TaskTeardown(context=>System.Console.WriteLine("::endgroup::"));
}

Setup(context=>{
    const string applicationName = "MyApp";
    const string publisher = "devlead";
    const string publishUrl = "https://clickoncenet5test.blob.core.windows.net/publish";
    const string configuration = "Release";

    var buildDate = DateTime.UtcNow;
    var timeOfDay = (short)((buildDate - buildDate.Date).TotalSeconds/3);
    var version = $"{buildDate:yyyy}.{buildDate:MM}.{buildDate:dd}.{timeOfDay}";

    context.Information("Setting up version {0}", version);

    var artifactsDirectory = context.MakeAbsolute(context.Directory("./artifacts"));

    var publishDirectory = artifactsDirectory.Combine($"{applicationName}.{version}");

    return new BuildData(
        version,
        configuration,
        new DotNetCoreMSBuildSettings()
                                .WithProperty("Version", version)
                                .WithProperty("Configuration", configuration),
        new ClickOnceData(
            applicationName,
            publisher,
            publishUrl,
            publishDirectory.GetDirectoryName(),
            version
        ),
        context.MakeAbsolute(context.Directory("./src")),
        artifactsDirectory,
        publishDirectory,
        new StorageAccount(
            context.EnvironmentVariable("PUBLISH_STORAGE_ACCOUNT"),
            context.EnvironmentVariable("PUBLISH_STORAGE_CONTAINER"),
            context.EnvironmentVariable("PUBLISH_STORAGE_KEY")
        )
    );
});

Task("Clean")
    .Does<BuildData>(
        (context, data) => {
            context.CleanDirectories(
                new []{
                    data.ArtifactsDirectory,
                    data.PublishDirectory
                });
            context.CleanDirectories("./src/**/bin/" + data.Configuration);
            context.CleanDirectories("./src/**/obj");
        }
    );

Task("Restore")
    .IsDependentOn("Clean")
    .Does<BuildData>(
        (context, data)=> {
            context.DotNetCoreRestore(
                data.SolutionDirectory.FullPath,
                new DotNetCoreRestoreSettings {
                    MSBuildSettings = data.MSBuildSettings
                }
            );
        }
    );

Task("Build")
    .IsDependentOn("Restore")
    .Does<BuildData>(
        (context, data)=> {
            context.DotNetCoreBuild(
                data.SolutionDirectory.FullPath,
                new DotNetCoreBuildSettings {
                    NoRestore = true,
                    MSBuildSettings = data.MSBuildSettings
                }
            );
        }
    );

Task("Publish")
    .IsDependentOn("Build")
    .Does<BuildData>(
        (context, data)=> {
            context.DotNetCorePublish(
                data.SolutionDirectory.FullPath,
                new DotNetCorePublishSettings {
                    NoRestore = true,
                    NoBuild = true,
                    OutputDirectory = data.PublishDirectory,
                    MSBuildSettings = data.MSBuildSettings
                }
            );
        }
    );

Task("ClickOnce-Launcher")
    .IsDependentOn("Publish")
    .Does<BuildData>(
        (context, data)=> {
            context.MageToolAddLauncher(
                    data.ArtifactsDirectory,
                   data.ClickOnceData
                );
        }
    );

Task("ClickOnce-Application-Manifest")
    .IsDependentOn("ClickOnce-Launcher")
    .Does<BuildData>(
        (context, data)=> {
            context.MageToolNewApplication(
                    data.ArtifactsDirectory,
                    data.ClickOnceData
                );
        }
    );

Task("ClickOnce-Deployment-Manifest")
    .IsDependentOn("ClickOnce-Application-Manifest")
    .Does<BuildData>(
        (context, data)=> {
            context.MageToolNewDeployment(
                    data.ArtifactsDirectory,
                    data.ClickOnceData
            );
        }
    );

Task("ClickOnce-Deployment-UpdateManifest")
    .IsDependentOn("ClickOnce-Deployment-Manifest")
    .Does<BuildData>(
        (context, data)=> {
            context.MageToolUpdateDeploymentMinVersion(
                    data.ArtifactsDirectory,
                    data.ClickOnceData
            );
        }
    );

Task("ClickOnce-Deployment-CreateAppRef")
    .IsDependentOn("ClickOnce-Deployment-UpdateManifest")
    .Does<BuildData>(
        (context, data)=> {
            context.CreateAppRef(
                    data.ClickOnceData,
                    data.ArtifactsDirectory
            );
        }
    );

Task("ClickOnce-Upload-Version")
    .IsDependentOn("ClickOnce-Deployment-CreateAppRef")
    .WithCriteria<BuildData>((context, data) => data.ShouldPublish)
    .Does<BuildData>(
        async (context, data)=> {
            await System.Threading.Tasks.Task.WhenAll(
                context
                    .GetFiles($"{data.PublishDirectory}/**/*.*")
                    .Select(file=> context.UploadToBlobStorage(
                    data.StorageAccount,
                    file,
                    data.ArtifactsDirectory.GetRelativePath(file)
                    ))
            );
        }
    );

Task("ClickOnce-Upload-Application")
    .IsDependentOn("ClickOnce-Upload-Version")
    .WithCriteria<BuildData>((context, data) => data.ShouldPublish)
    .Does<BuildData>(
        async (context, data)=> {
           await System.Threading.Tasks.Task.WhenAll(
                context
                    .GetFiles($"{data.ArtifactsDirectory}/*.*")
                    .Select(file=> context.UploadToBlobStorage(
                    data.StorageAccount,
                    file,
                    data.ArtifactsDirectory.GetRelativePath(file)
                    ))
            );
        }
    );



Task("Default")
    .IsDependentOn("ClickOnce-Deployment-Manifest");

Task("Publish-ClickOnce")
    .IsDependentOn("ClickOnce-Upload-Application");

RunTarget(target);