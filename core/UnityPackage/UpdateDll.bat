pushd %~dp0

SET SRC=..\..\..\Akka.Interfaced\core\Akka.Interfaced-SlimClient.Net35\bin\Release
SET DST=.\Assets\Middlewares\AkkaInterfaced
SET PDB2MDB=..\..\tools\unity3d\pdb2mdb.exe

%PDB2MDB% "%SRC%\Akka.Interfaced-Base.dll"
%PDB2MDB% "%SRC%\Akka.Interfaced-SlimClient.dll"

COPY /Y %SRC%\Akka.Interfaced-Base.dll %DST%
COPY /Y %SRC%\Akka.Interfaced-Base.dll.mdb %DST%
COPY /Y %SRC%\Akka.Interfaced-SlimClient.dll %DST%
COPY /Y %SRC%\Akka.Interfaced-SlimClient.dll.mdb %DST%

popd
