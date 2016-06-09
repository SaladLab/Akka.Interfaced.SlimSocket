#I @"packages/FAKE/tools"
#I @"packages/FAKE.BuildLib/lib/net451"
#r "FakeLib.dll"
#r "BuildLib.dll"

open Fake
open BuildLib

let solution = 
    initSolution
        "./Akka.Interfaced.SlimSocket.sln" "Release" 
        [ { emptyProject with Name = "Akka.Interfaced.SlimSocket.Base"
                              Folder = "./core/Akka.Interfaced.SlimSocket.Base"
                              PackagePrerelease = "beta"
                              Dependencies = 
                                  [ ("protobuf-net", "")
                                    ("TypeAlias", "") ] }
          { emptyProject with Name = "Akka.Interfaced.SlimSocket.Client"
                              Folder = "./core/Akka.Interfaced.SlimSocket.Client"
                              PackagePrerelease = "beta"
                              Dependencies = 
                                  [ ("Akka.Interfaced.SlimSocket.Base", "")
                                    ("Akka.Interfaced-SlimClient", "")
                                    ("Common.Logging.Core", "")
                                    ("NetLegacySupport.Tuple", "") ] }
          { emptyProject with Name = "Akka.Interfaced.SlimSocket.Server"
                              Folder = "./core/Akka.Interfaced.SlimSocket.Server"
                              PackagePrerelease = "beta"
                              Dependencies = 
                                  [ ("Akka.Interfaced.SlimSocket.Base", "")
                                    ("Akka.Interfaced", "")
                                    ("Common.Logging.Core", "") ] } ]

Target "Clean" <| fun _ -> cleanBin

Target "AssemblyInfo" <| fun _ -> generateAssemblyInfo solution

Target "Restore" <| fun _ -> restoreNugetPackages solution

Target "Build" <| fun _ -> buildSolution solution

Target "Test" <| fun _ -> testSolution solution

Target "Cover" <| fun _ ->
     coverSolutionWithParams 
        (fun p -> { p with Filter = "+[Akka.Interfaced.Slim*]* -[*.Tests]*" })
        solution
    
Target "Coverity" <| fun _ -> coveritySolution solution "SaladLab/Akka.Interfaced.SlimSocket"

Target "PackNuget" <| fun _ -> createNugetPackages solution

Target "PackUnity" <| fun _ ->
    packUnityPackage "./core/UnityPackage/AkkaInterfacedSlimSocket.unitypackage.json"

Target "Pack" <| fun _ -> ()

Target "PublishNuget" <| fun _ -> publishNugetPackages solution

Target "PublishUnity" <| fun _ -> ()

Target "Publish" <| fun _ -> ()

Target "CI" <| fun _ -> ()

Target "DevLink" <| fun _ ->
    devlink "./packages" [ "../Akka.Interfaced" ]

Target "Help" <| fun _ -> 
    showUsage solution (fun _ -> None)

"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "Build"
  ==> "Test"

"Build" ==> "Cover"
"Restore" ==> "Coverity"

let isPublishOnly = getBuildParam "publishonly"

"Build" ==> "PackNuget" =?> ("PublishNuget", isPublishOnly = "")
"Build" ==> "PackUnity" =?> ("PublishUnity", isPublishOnly = "")
"PackNuget" ==> "Pack"
"PackUnity" ==> "Pack"
"PublishNuget" ==> "Publish"
"PublishUnity" ==> "Publish"

"Test" ==> "CI"
"Cover" ==> "CI"
"Publish" ==> "CI"

RunTargetOrDefault "Help"
