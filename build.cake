#load "nuget:?package=Cake.ClickOnce.Recipe&version=0.3.0"

ClickOnce.ApplicationName = "MyApp";
ClickOnce.Publisher = "devlead";
ClickOnce.PublishUrl = "https://clickoncenet5test.blob.core.windows.net/publish";
ClickOnce.RunBuild();

DotNetCoreTool(
    "tool",
    new DotNetCoreToolSettings {
        ArgumentCustomization = args => args
                                            .Append("run")
                                            .Append("dpi")
                                            .Append("nuget")
                                            .Append("--silent")
                                            .AppendSwitchQuoted("--output", "table")
                                            .Append(
                                                (
                                                    !string.IsNullOrWhiteSpace(EnvironmentVariable("NuGetReportSettings_SharedKey"))
                                                    &&
                                                    !string.IsNullOrWhiteSpace(EnvironmentVariable("NuGetReportSettings_WorkspaceId"))
                                                )
                                                    ? "report"
                                                    : "analyze"
                                                )
                                            .AppendSwitchQuoted("--buildversion", ClickOnce.Version),
    }
);