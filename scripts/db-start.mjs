import {existsSync,mkdirSync,writeFileSync,readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {randomBytes} from 'node:crypto';
import {spawnSync} from 'node:child_process';
const dir=resolve('.runtime');mkdirSync(dir,{recursive:true,mode:0o700});
const envFile=resolve(dir,'sql.env');const config=resolve(dir,'config.json');
if(!existsSync(envFile)){const password='Eto_9!'+randomBytes(24).toString('base64url');writeFileSync(envFile,`ACCEPT_EULA=Y\nMSSQL_SA_PASSWORD=${password}\nMSSQL_PID=Developer\n`,{mode:0o600});writeFileSync(config,JSON.stringify({ConnectionStrings:{Portal:`Server=127.0.0.1,1433;Database=EtoPortal;User Id=sa;Password=${password};Encrypt=True;TrustServerCertificate=True;Connect Timeout=30`}},null,2),{mode:0o600});}
if(!existsSync(config))throw Error('Database config is missing. Restore .runtime/config.json; do not regenerate credentials for an existing volume.');
const env={...process.env};for(const name of ['DOCKER_HOST','DOCKER_CONTEXT','DOCKER_TLS','DOCKER_TLS_VERIFY','DOCKER_CERT_PATH'])delete env[name];
function docker(args,quiet=false){const r=spawnSync('docker',['--host=unix:///var/run/docker.sock',...args],{encoding:'utf8',env});if(r.status!==0)throw Error(r.stderr || 'Docker failed');if(!quiet&&r.stdout)process.stdout.write(r.stdout);return r.stdout.trim();}
const existing=spawnSync('docker',['--host=unix:///var/run/docker.sock','inspect','etoxd-sql'],{env,encoding:'utf8'});
if(existing.status===0){const container=JSON.parse(existing.stdout)[0];if(container.Config.Labels?.['app.etoxd']!=='development')throw Error('The name etoxd-sql belongs to another container.');docker(['start','etoxd-sql']);}
else docker(['run','-d','--name','etoxd-sql','--label','app.etoxd=development','--memory','4g','--env-file',envFile,'-p','127.0.0.1:1433:1433','-v','etoxd-sql-data:/var/opt/mssql','mcr.microsoft.com/mssql/server@sha256:4525e3efac9f3e241cb003bb2ff2a82818b502f37e12049d788b1a9fa7e13b7f']);
console.log('SQL Server is starting on loopback port 1433. Data is retained in the etoxd-sql-data Docker volume.');
