import {existsSync} from 'node:fs';
import {spawn} from 'node:child_process';
const dotnet=process.env.DOTNET_ROOT?process.env.DOTNET_ROOT+'/dotnet':existsSync('/workspace/.dotnet/dotnet')?'/workspace/.dotnet/dotnet':'dotnet';
const child=spawn(dotnet,['build','api/Eto.Api.csproj','--disable-build-servers','-p:RestoreLockedMode=true'],{stdio:'inherit',env:{DOTNET_CLI_HOME:'/workspace/.dotnet-home',NUGET_PACKAGES:'/workspace/.nuget',DOTNET_CLI_TELEMETRY_OPTOUT:'1',...process.env}});
child.on('exit',code=>process.exit(code || 0));
