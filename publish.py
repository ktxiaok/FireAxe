import os
import subprocess
from send2trash import send2trash
import shutil
from xml.etree import ElementTree

class VersionNotFoundException(Exception):
    pass

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

def publish(context: dict[str, str], runtime: str):
    version = context['version']
    bin_dir = 'bin'
    publish_dir = os.path.join(bin_dir, 'publish')
    output_dir = os.path.join(publish_dir, f'{version}-{runtime}')

    if os.path.exists(output_dir):
        send2trash(output_dir)

    subprocess.run([
        'dotnet', 'publish',
        os.path.join(os.getcwd(), 'FireAxe.GUI'),
        '--output', output_dir,
        '--configuration', 'Release',
        '--runtime', runtime,
        '--property:PublishReadyToRun=true',
        '--self-contained',
    ])

    archive_type = 'zip'
    archive_file_noext = os.path.join(publish_dir, f'FireAxe-{version}-{runtime}')
    archive_file = archive_file_noext + '.' + archive_type

    if os.path.exists(archive_file):
        send2trash(archive_file)

    shutil.make_archive(archive_file_noext, archive_type, output_dir)

if __name__ == '__main__':
    context = get_context()
    publish(context, 'win-x64')
    print('Done')