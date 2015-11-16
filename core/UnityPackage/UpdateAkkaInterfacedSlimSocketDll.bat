pushd %~dp0

SET SRC=..\Akka.Interfaced.SlimSocket.Client.Net35\bin\Release
SET DST=.\Assets\Middlewares\AkkaInterfaced
SET PDB2MDB=..\..\tools\unity3d\pdb2mdb.exe

%PDB2MDB% "%SRC%\Akka.Interfaced.SlimSocket.Base.dll"
%PDB2MDB% "%SRC%\Akka.Interfaced.SlimSocket.Client.dll"

COPY /Y %SRC%\Akka.Interfaced.SlimSocket.Base.dll* %DST%
COPY /Y %SRC%\Akka.Interfaced.SlimSocket.Client.dll* %DST%

popd
