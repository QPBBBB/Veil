"""Convert and verify Mixamo FBX skeletons. Unity C# owns animation import and .anim output."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import uuid

HERE = Path(__file__).resolve().parent
# Find the host repository's shared SDK without relying on the package depth.
REPO = next((p for p in HERE.parents if (p / "Tools/SDK/FBX/include/fbxsdk.h").is_file()), HERE)
DEFAULT_PROJECT = REPO / "UnityProject"


def run(args, cwd=None):
    result = subprocess.run([str(a) for a in args], cwd=cwd, capture_output=True,
                            encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError((result.stderr + "\n" + result.stdout).strip())
    return result.stdout


def sha(path):
    with open(path, "rb") as stream:
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def build_helper(sdk):
    if os.name != "nt":
        raise RuntimeError("The bundled FBX SDK requires Windows x64.")
    source = HERE / "native" / "converter.cpp"
    library = sdk / "lib/x64/release/libfbxsdk.lib"
    dll = library.with_suffix(".dll")
    for path in (sdk / "include/fbxsdk.h", library, dll):
        if not path.is_file():
            raise RuntimeError("Missing FBX SDK file: " + str(path))
    cache = HERE / ".build"
    cache.mkdir(exist_ok=True)
    executable = cache / "converter.exe"
    stamp = cache / "source.sha256"
    fingerprint = sha(source) + sha(library) + sha(sdk / "include/fbxsdk.h")
    if not executable.exists() or not stamp.exists() or stamp.read_text() != fingerprint:
        vswhere = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Microsoft Visual Studio/Installer/vswhere.exe"
        if not vswhere.exists():
            raise RuntimeError("Visual Studio C++ build tools not found. No tools were installed.")
        installation = run([vswhere, "-latest", "-products", "*", "-requires",
                            "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                            "-property", "installationPath"]).strip()
        vcvars = Path(installation) / "VC/Auxiliary/Build/vcvars64.bat"
        if not installation or not vcvars.is_file():
            raise RuntimeError("Visual Studio x64 C++ compiler is required for the native SDK bridge.")
        def quoted(path):
            return '"' + str(path).replace("%", "%%") + '"'
        script = cache / "build.cmd"
        script.write_text(
            "@echo off\nsetlocal DisableDelayedExpansion\ncall " + quoted(vcvars) + " >nul\n"
            "if errorlevel 1 exit /b 1\n"
            "cl /nologo /utf-8 /std:c++17 /EHsc /MD /DFBXSDK_SHARED /I" + quoted(sdk / "include") + " " +
            quoted(source) + " /Fe:" + quoted(executable) + " /link /LIBPATH:" +
            quoted(library.parent) + " libfbxsdk.lib\nexit /b %errorlevel%\n", encoding="utf-8")
        print("Building the native FBX SDK bridge...", flush=True)
        run(["cmd.exe", "/d", "/c", script], cwd=cache)
        stamp.write_text(fingerprint)
    if not (cache / dll.name).exists() or sha(cache / dll.name) != sha(dll):
        shutil.copy2(dll, cache / dll.name)
    return executable


def verify_snapshots(before, after):
    a, b = json.loads(before.read_text(encoding="utf-8")), json.loads(after.read_text(encoding="utf-8"))
    if a.keys() != b.keys():
        raise RuntimeError("Export changed skeleton paths or animation bindings: " +
                           str(sorted(a.keys() ^ b.keys())[:8]))
    for key in a:
        if len(a[key]) != len(b[key]):
            raise RuntimeError("Export changed data length: " + key)
        for index, (x, y) in enumerate(zip(a[key], b[key])):
            if not math.isclose(x, y, rel_tol=1e-7, abs_tol=1e-8):
                raise RuntimeError("Export validation failed at {}[{}]: {} != {}".format(key, index, x, y))
    return {"records": len(a), "bone_curves": sum(k.startswith("curve:") for k in a)}


def resolve_input(value, project):
    path = Path(value)
    if not path.is_absolute():
        path = project / path if path.parts and path.parts[0] == "Assets" else Path.cwd() / path
    path = path.resolve()
    if not path.is_file() or path.suffix.lower() != ".fbx":
        raise RuntimeError("Input must be an existing .fbx file: " + str(path))
    return path


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", help="FBX path, absolute or relative; Assets/... is relative to the Unity project")
    parser.add_argument("--output", type=Path, help="Output skeleton-only .fbx")
    parser.add_argument("--project", type=Path, default=DEFAULT_PROJECT)
    parser.add_argument("--sdk", type=Path, default=REPO / "Tools/SDK/FBX")
    parser.add_argument("--no-import", action="store_true", help="Compatibility flag; Python always performs conversion only")
    parser.add_argument("--dry-run", action="store_true", help="Verify in temporary storage without writing output")
    parser.add_argument("--in-place", action="store_true", help="Replace input after validation and automatic backup")
    args = parser.parse_args(argv)
    source = resolve_input(args.input, args.project.resolve())
    if args.in_place and args.output:
        parser.error("--in-place cannot be combined with --output")
    if not args.output and not args.in_place and not args.dry_run:
        parser.error("Specify --output for the converted skeleton FBX")
    destination = source if args.in_place else (args.output or source.with_name(source.stem + "_Veil.fbx")).resolve()
    if destination.suffix.lower() != ".fbx":
        parser.error("Output must have the .fbx extension")
    if not args.in_place and destination.exists():
        raise RuntimeError("Output already exists; not overwritten: " + str(destination))
    if destination == source and not args.in_place:
        raise RuntimeError("Use --in-place explicitly to replace the source.")
    helper = build_helper(args.sdk.resolve())
    source_hash = sha(source)
    with tempfile.TemporaryDirectory(prefix="veil-mixamo-") as temp:
        work = Path(temp)
        staging = work / "converted.fbx"
        report_file, before, after = work / "report.json", work / "before.json", work / "after.json"
        print("Converting and verifying skeleton/animation data...", flush=True)
        run([helper, source, staging, report_file, before, after])
        report = json.loads(report_file.read_text(encoding="utf-8"))
        report["verification"] = verify_snapshots(before, after)
        report.update(input=str(source), output=str(destination))
        if not report["verification"]["bone_curves"]:
            report["warning"] = "No bone animation curves were found; output may be a static pose."
        if args.dry_run:
            report["dry_run"] = True
            print(json.dumps(report, ensure_ascii=False, indent=2))
            return 0
        if sha(source) != source_hash:
            raise RuntimeError("Input changed during conversion; no output written.")
        if args.in_place:
            backup = HERE / ".backups" / uuid.uuid4().hex
            backup.mkdir(parents=True)
            shutil.copy2(source, backup / source.name)
            meta = Path(str(source) + ".meta")
            if meta.exists():
                shutil.copy2(meta, backup / meta.name)
            report["backup"] = str(backup)
        destination.parent.mkdir(parents=True, exist_ok=True)
        # Publish only complete files so Unity never sees a partially written FBX.
        # On Windows rename refuses an existing target; replace is explicitly opt-in.
        swap = destination.parent / ("." + destination.name + "." + uuid.uuid4().hex + ".tmp")
        try:
            shutil.copyfile(staging, swap)
            if args.in_place:
                os.replace(swap, destination)
            else:
                os.rename(swap, destination)
        finally:
            if swap.exists():
                swap.unlink()
        print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, OSError, ValueError, KeyError) as error:
        print("ERROR: " + str(error), file=sys.stderr)
        sys.exit(1)
