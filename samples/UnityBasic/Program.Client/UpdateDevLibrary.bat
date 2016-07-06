@echo off

SET PDB2MDB=..\..\..\tools\unity3d\pdb2mdb.exe
SET SRCDIR=..\..\..\core\Akka.Interfaced.SlimSocket.Client.Net35\bin\Debug
SET OUTDIR=.\Assets\UnityPackages\AkkaInterfacedSlimSocket

%PDB2MDB% %SRCDIR%\Akka.Interfaced.SlimSocket.Base.dll 
%PDB2MDB% %SRCDIR%\Akka.Interfaced.SlimSocket.Client.dll 

COPY /Y %SRCDIR%\Akka.Interfaced.SlimSocket.Base.dll* %OUTDIR%
COPY /Y %SRCDIR%\Akka.Interfaced.SlimSocket.Client.dll* %OUTDIR%

pause.
