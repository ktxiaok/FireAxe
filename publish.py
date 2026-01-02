from typing import Optional
import os
import stat
import subprocess
from send2trash import send2trash
import shutil
from xml.etree import ElementTree

class VersionNotFoundException(Exception):
    pass

def bool_to_str(value: bool) -> str:
    return 'true' if value else 'false'

def copy_all_in_dir(src_dir: str, dst_dir: str):
    os.makedirs(dst_dir, exist_ok=True)
    for filename in os.listdir(src_dir):
        file = os.path.join(src_dir, filename)
        if os.path.isdir(file):
            shutil.copytree(file, os.path.join(dst_dir, filename))
        else:
            shutil.copy(file, dst_dir)

def get_project_version(project: ElementTree.Element) -> str:
    for property_group in project.findall('PropertyGroup'):
        version = property_group.find('Version')
        if version != None:
            return version.text
    raise VersionNotFoundException()

def get_context() -> dict[str, str]:
    context = {}
    directory_build_props = ElementTree.parse('Directory.Build.props').getroot()
    context['version'] = get_project_version(directory_build_props)
    return context

def publish(context: dict[str, str], runtime: Optional[str]):
    self_contained_str = bool_to_str(runtime != None)
    runtime_name = runtime if runtime != None else 'portable'

    version = context['version']
    bin_dir = 'bin'
    publish_dir = os.path.join(bin_dir, 'publish')
    output_dir = os.path.join(publish_dir, f'{version}-{runtime_name}')

    if os.path.exists(output_dir):
        send2trash(output_dir)
     
    process_args = [
        'dotnet', 'publish',
        os.path.join(os.getcwd(), 'FireAxe.GUI'),
        '--output', output_dir,
        '--configuration', 'Release',
        '--property:PublishReadyToRun=true',
        '--self-contained', self_contained_str,
    ]
    if runtime != None:
        process_args += ['--runtime', runtime]
    subprocess.run(process_args)

    archive_type = 'zip'
    archive_file_noext = os.path.join(publish_dir, f'FireAxe-{version}-{runtime_name}')
    archive_file = archive_file_noext + '.' + archive_type

    if os.path.exists(archive_file):
        send2trash(archive_file)

    shutil.make_archive(archive_file_noext, archive_type, output_dir)

    # AppImage
    if runtime != None and runtime.startswith('linux'):
        appdir = os.path.join('PublishContents', 'Linux', 'AppImage', 'FireAxe.AppDir')
        appdir_bin = os.path.join(appdir, 'usr', 'bin')
        appimage_file = os.path.join(publish_dir, f'FireAxe-{version}-{runtime_name}.AppImage')

        if os.path.exists(appdir_bin):
            send2trash(appdir_bin)
        copy_all_in_dir(output_dir, appdir_bin)

        os.chmod(os.path.join(appdir, 'AppRun'), 
                 stat.S_IRUSR | stat.S_IRGRP | stat.S_IROTH | stat.S_IWUSR | stat.S_IWGRP | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
        os.chmod(os.path.join(appdir, 'usr', 'bin', 'FireAxe'), 
                 stat.S_IRUSR | stat.S_IRGRP | stat.S_IROTH | stat.S_IWUSR | stat.S_IWGRP | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)

        subprocess.run(['appimagetool', appdir, appimage_file])