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
                              Dependencies = 
                                  [ ("protobuf-net", "")
                                    ("TypeAlias", "") ] }
          { emptyProject with Name = "Akka.Interfaced.SlimSocket.Client"
                              Folder = "./core/Akka.Interfaced.SlimSocket.Client"
                              Dependencies = 
                                  [ ("Akka.Interfaced.SlimSocket.Base", "")
                                    ("Akka.Interfaced-SlimClient", "")
                                    ("Common.Logging.Core", "")
                                    ("NetLegacySupport.Tuple", "") ] }
          { emptyProject with Name = "Akka.Interfaced.SlimSocket.Server"
                              Folder = "./core/Akka.Interfaced.SlimSocket.Server"
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

Target "Nuget" <| fun _ ->
    createNugetPackages solution
    publishNugetPackages solution

Target "CreateNuget" <| fun _ ->
    createNugetPackages solution

Target "PublishNuget" <| fun _ ->
    publishNugetPackages solution

Target "Unity" <| fun _ -> buildUnityPackage "./core/UnityPackage"

Target "CI" <| fun _ -> ()

Target "Help" <| fun _ -> 
    showUsage solution (fun _ -> None)

"Clean"
  ==> "AssemblyInfo"
  ==> "Restore"
  ==> "Build"
  ==> "Test"

"Build" ==> "Nuget"
"Build" ==> "CreateNuget"
"Build" ==> "Cover"
"Restore" ==> "Coverity"

"Test" ==> "CI"
"Cover" ==> "CI"
"Nuget" ==> "CI"

RunTargetOrDefault "Help"
