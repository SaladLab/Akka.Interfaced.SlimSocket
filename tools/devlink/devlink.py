import sys
import os
import shutil
import glob
import zipfile
import ctypes


def find_package_path(dir, id):
    for a in glob.glob(os.path.join(dir, id + ".[0-9]*")):
        return a


def do_link(packages, package_dir, project_root):
    kdll = ctypes.windll.LoadLibrary("kernel32.dll")
    for id, contents in packages.iteritems():
        print "***", id

        package_path = find_package_path(package_dir, id)
        if package_path is None:
            print "Cannot find package: " + id
            continue

        for c_pdir, c_sdir in contents:
            pdir = os.path.join(package_path, c_pdir)
            sdir = os.path.join(project_root, c_sdir)
            for file in set(os.listdir(pdir)).intersection(os.listdir(sdir)):
                print "-", file
                src_file = os.path.join(sdir, file)
                dst_file = os.path.join(pdir, file)
                os.remove(dst_file)
                kdll.CreateHardLinkW(unicode(dst_file), unicode(src_file), 0)

                
def do_unlink(packages, package_dir):
    for id, contents in packages.iteritems():
        print "***", id

        package_path = find_package_path(package_dir, id)
        if package_path is None:
            print "Cannot find package: " + id
            continue

        nupkgs = glob.glob(os.path.join(package_path, "*.nupkg"))
        if len(nupkgs) == 0:
            print "Cannot find nuget package: " + id
        nupkg = nupkgs[0]
        print "-", nupkg

        for c_pdir, _ in contents:
            pdir = os.path.join(package_path, c_pdir)
            if os.path.exists(pdir):
                shutil.rmtree(pdir)

        zf = zipfile.ZipFile(nupkg)
        for name in zf.namelist():
            if any(name.startswith(c_pdir + "/") for c_pdir, _ in contents):
                zf.extract(name, package_path)


def run(packages, package_dir, project_root):
    if len(sys.argv) < 2:
        print "[Usage] " + os.path.split(sys.argv[0])[1] + " [link|unlink]"
        return 1
    cmd = sys.argv[1].lower()
    if cmd == "link":
        do_link(packages, package_dir, project_root)
    elif cmd == "unlink":
        do_unlink(packages, package_dir)
    else:
        print "Invalid command: " + cmd
        return 1

 
# ----------


packages = {
  # Akka.Interfaced
  "Akka.Interfaced":
    [ ( "lib/net45", "./Akka.Interfaced/core/Akka.Interfaced/bin/Debug" ) ],
  "Akka.Interfaced-Base":
    [ ( "lib/net45", "./Akka.Interfaced/core/Akka.Interfaced-Base/bin/Debug" ),
      ( "lib/net35", "./Akka.Interfaced/core/Akka.Interfaced-Base.Net35/bin/Debug" ) ],
  "Akka.Interfaced-SlimClient":
    [ ( "lib/net45", "./Akka.Interfaced/core/Akka.Interfaced-SlimClient/bin/Debug" ),
      ( "lib/net35", "./Akka.Interfaced/core/Akka.Interfaced-SlimClient.Net35/bin/Debug" ) ],
  "Akka.Interfaced.Templates-Protobuf": 
    [ ( "tools", "./Akka.Interfaced/core/CodeGenerator/bin/Debug" ) ],
  "Akka.Interfaced-SlimClient.Templates":
    [ ( "tools", "./Akka.Interfaced/core/CodeGenerator/bin/Debug" ) ],
  "Akka.Interfaced-SlimClient.Templates-Protobuf":
    [ ( "tools", "./Akka.Interfaced/core/CodeGenerator/bin/Debug" ) ],
}


run(packages, "../../packages", "../../../")
