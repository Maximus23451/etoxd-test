#!/usr/bin/env bash
set -euo pipefail
sdk_target="${1:-/workspace/.dotnet}"
if [ -x "$sdk_target/dotnet" ] && [ "$("$sdk_target/dotnet" --version)" = '10.0.404' ]; then
  echo '.NET SDK 10.0.404 is already installed.'
  exit 0
fi
python3 - "$sdk_target" <<'PY'
import hashlib,json,os,pathlib,shutil,sys,tarfile,tempfile,urllib.request
version='10.0.404'
target=pathlib.Path(sys.argv[1]); target.mkdir(parents=True,exist_ok=True)
metadata=json.load(urllib.request.urlopen('https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'))
release=next(r['sdk'] for r in metadata['releases'] if r.get('sdk',{}).get('version')==version)
package=next(f for f in release['files'] if f['rid']=='linux-x64' and f['url'].endswith('.tar.gz'))
with tempfile.TemporaryDirectory(dir=target.parent) as temporary:
 archive_path=pathlib.Path(temporary)/'sdk.tar.gz'; digest=hashlib.sha512()
 with urllib.request.urlopen(package['url']) as source,archive_path.open('wb') as output:
  while chunk:=source.read(1024*1024):output.write(chunk);digest.update(chunk)
 if digest.hexdigest().lower()!=package['hash'].lower():raise RuntimeError('Official .NET SHA-512 verification failed.')
 stage=pathlib.Path(temporary)/'extracted';stage.mkdir()
 with tarfile.open(archive_path) as archive:archive.extractall(stage,filter='data')
 # Replace the host atomically, including when another process still uses its old inode.
 host=stage/'dotnet';shutil.copy2(host,target/'dotnet.new');host.unlink()
 shutil.copytree(stage,target,dirs_exist_ok=True);os.replace(target/'dotnet.new',target/'dotnet')
print('.NET SDK '+version+' installed with matching official SHA-512.')
PY
