#!/usr/bin/env python3
"""Compile against installed Unity references; run pure model checks without launching Unity.

No packages or project settings are changed. Outputs are kept in a temporary directory.
The offline model run uses tiny storage/random/resource adapters, not the Unity engine.
"""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile

UNITY_DEFAULT = Path('/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents')
PROJECT = Path(__file__).resolve().parents[2]


def run(command):
    result = subprocess.run([str(x) for x in command])
    if result.returncode:
        raise SystemExit(result.returncode)


def compile_sources(mono, compiler, output, sources, references=(), defines=(), exe=False):
    args = [mono, compiler, '/nologo', '/langversion:9.0',
            '/target:' + ('exe' if exe else 'library'), '/out:' + str(output)]
    args.extend('/reference:' + str(p) for p in references)
    if defines:
        args.append('/define:' + ';'.join(defines))
    args.extend(sorted(sources))
    run(args)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', type=Path, default=UNITY_DEFAULT,
                        help='Unity.app/Contents directory (Unity 2022.3)')
    parser.add_argument('--ui-assembly', type=Path,
                        help='UnityEngine.UI.dll from the same Unity version')
    parser.add_argument('--compile-only', action='store_true')
    args = parser.parse_args()
    mono = args.unity / 'MonoBleedingEdge/bin/mono'
    compiler = args.unity / 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
    managed = args.unity / 'Managed'
    if not mono.exists() or not compiler.exists():
        parser.error('Installed Unity Mono compiler not found; supply --unity.')
    ui = args.ui_assembly
    if ui is None:
        templates = args.unity / 'Resources/PackageManager/ProjectTemplates/libcache'
        ui = next(templates.glob('*/ScriptAssemblies/UnityEngine.UI.dll'), None)
    if ui is None or not ui.exists():
        parser.error('UI assembly not found; supply --ui-assembly from Unity 2022.3.')
    engine_refs = sorted((managed / 'UnityEngine').glob('UnityEngine*.dll'))
    engine_refs += [ui, args.unity / "MonoBleedingEdge/lib/mono/unityjit-macos/Facades/netstandard.dll"]
    runtime = list((PROJECT / 'Assets/Scripts').rglob('*.cs'))
    editor = list((PROJECT / 'Assets/Editor').rglob('*.cs'))
    tests = list((PROJECT / 'Assets/Tests/EditMode/Editor').glob('*.cs'))
    with tempfile.TemporaryDirectory(prefix='gunya-verify-') as tmp:
        tmp = Path(tmp)
        compile_sources(mono, compiler, tmp / 'Runtime.dll', runtime, engine_refs)
        print('PASS production player scripts compile against Unity references', flush=True)
        editor_refs = engine_refs
        editor_refs += sorted((managed / 'UnityEngine').glob('UnityEditor*.dll'))
        compile_sources(mono, compiler, tmp / 'Editor.dll', runtime + editor + tests,
                        editor_refs, ['UNITY_EDITOR'])
        print('PASS production editor scripts and EditMode checks compile', flush=True)
        if args.compile_only:
            return
        # Use the production enum verbatim, not a second list of play outcomes.
        fielding = (PROJECT / 'Assets/Scripts/Batting/FieldingModel.cs').read_text()
        kind = re.search(r'public enum PlayKind\s*\{[^}]*\}', fielding).group(0)
        adapter = tmp / 'PlayKind.cs'
        adapter.write_text(kind)
        logic = [PROJECT / ('Assets/Scripts/' + p) for p in [
            'Cards/CharaCatalog.cs', 'Cards/Gacha.cs', 'Cards/Collection.cs',
            'Batting/GameState.cs']]
        compile_sources(mono, compiler, tmp / 'Models.exe', logic + [
            PROJECT / 'Assets/Tests/EditMode/Editor/RegressionChecks.cs', adapter,
            PROJECT / 'Tools/verification/OfflineAdapters.cs'], defines=['GUNYA_OFFLINE_TESTS'], exe=True)
        run([mono, tmp / 'Models.exe', PROJECT / 'Assets/Resources'])
        print('PASS offline model checks (engine behavior still requires Unity)', flush=True)


if __name__ == '__main__':
    main()
