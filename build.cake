#load "nuget:?package=Cake.ClickOnce.Recipe&version=0.1.0"

ClickOnce.ApplicationName = "MyApp";
ClickOnce.Publisher = "devlead";
ClickOnce.PublishUrl = "https://clickoncenet5test.blob.core.windows.net/publish";
ClickOnce.RunBuild();