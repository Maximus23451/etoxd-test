import {mkdtemp,readFile,mkdir,writeFile,rm} from 'node:fs/promises';
import {resolve} from 'node:path';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
export async function startFixture(port=3002){
  const directory=await mkdtemp(resolve('.runtime/test-'));const config=JSON.parse(await readFile('.runtime/config.json','utf8'));
  const database='EtoPortal_Test_'+Date.now()+'_'+Math.floor(Math.random()*10000);
  config.ConnectionStrings.Portal=config.ConnectionStrings.Portal.replace(/Database=[^;]+/i,'Database='+database);
  await writeFile(resolve(directory,'config.json'),JSON.stringify(config),{mode:0o600});
  const dotnet=process.env.DOTNET_ROOT?resolve(process.env.DOTNET_ROOT,'dotnet'):'/workspace/.dotnet/dotnet';
  const dll=resolve('api/bin/Debug/net10.0/Eto.Api.dll');
  const env={...process.env,APP_RUNTIME_DIR:directory,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:'http://127.0.0.1:'+port};
  let child=spawn(dotnet,[dll],{cwd:resolve('api'),env,stdio:['ignore','pipe','pipe']});let output='';child.stdout.on('data',b=>output+=b);child.stderr.on('data',b=>output+=b);
  const base='http://127.0.0.1:'+port;
  let ready=false;
  for(let n=0;n<160;n++){if(child.exitCode!==null)throw Error('Test API stopped: '+output.slice(-3000));try{if((await fetch(base+'/api/health')).ok){ready=true;break;}}catch{}await new Promise(r=>setTimeout(r,250));}
  if(!ready){child.kill();throw Error('Test API not ready: '+output.slice(-3000));}
  function client(){let cookie='';return async(path,method='GET',body)=>{const headers={...(cookie?{Cookie:cookie}:{}),...(body && !(body instanceof FormData)?{'Content-Type':'application/json'}:{})};const r=await fetch(base+'/api'+path,{method,headers,body:body?body instanceof FormData?body:JSON.stringify(body):undefined});const cookies=r.headers.getSetCookie();if(cookies.length)cookie=cookies.map(c=>c.split(';')[0]).join('; ');const data=await r.json().catch(()=>({}));return {status:r.status,data};};}
  async function restart(){
    child.kill('SIGTERM');await once(child,'exit');
    child=spawn(dotnet,[dll],{cwd:resolve('api'),env,stdio:['ignore','pipe','pipe']});child.stdout.on('data',b=>output+=b);child.stderr.on('data',b=>output+=b);
    for(let n=0;n<120;n++){try{if((await fetch(base+'/api/health')).ok)return;}catch{}await new Promise(r=>setTimeout(r,250));}
    throw Error('Restarted test API did not become ready.');
  }
  async function close(){if(child.exitCode===null && child.signalCode===null){child.kill('SIGTERM');await once(child,'exit');}const drop=spawn(dotnet,[dll,'--drop-test-db'],{cwd:resolve('api'),env,stdio:['ignore','pipe','pipe']});let error='';drop.stderr.on('data',b=>error+=b);const [code]=await once(drop,'exit');if(code!==0)throw Error('Could not remove isolated test database: '+error);await rm(directory,{recursive:true,force:true});}
  return {base,client,close,restart,directory};
}
