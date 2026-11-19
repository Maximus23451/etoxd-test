export class ApiError extends Error {
  constructor(status,message,requestId){super(message);this.status=status;this.requestId=requestId;}
}
export async function api(path,options={}) {
  let response;
  try {response=await fetch('/api'+path,{credentials:'same-origin',...options,headers:{...(!(options.body instanceof FormData) && options.body?{'Content-Type':'application/json'}:{}),...options.headers}});} catch {throw new ApiError(0,'Nem érjük el a szervert. Ellenőrizd a kapcsolatot, majd próbáld újra.');}
  const data=await response.json().catch(()=>({}));
  if(!response.ok)throw new ApiError(response.status,data.message || 'A kérés nem sikerült.',data.requestId);
  return data;
}
export const send=(path,data,method='POST')=>api(path,{method,body:data instanceof FormData?data:JSON.stringify(data)});
export function track(action,target=''){send('/events',{action,target}).catch(()=>{});}
export const zone='Europe/Budapest';
export const time=value=>new Intl.DateTimeFormat('hu-HU',{timeZone:zone,hour:'2-digit',minute:'2-digit'}).format(new Date(value));
export const dateKey=value=>{const parts=new Intl.DateTimeFormat('en-CA',{timeZone:zone,year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date(value));return ['year','month','day'].map(type=>parts.find(p=>p.type===type).value).join('-');};
export const dayLabel=value=>new Intl.DateTimeFormat('hu-HU',{timeZone:zone,month:'short',day:'numeric',weekday:'short'}).format(new Date(value));
export const shifted=(key,days)=>new Date(new Date(key+'T12:00:00Z').getTime()+days*86400000).toISOString().slice(0,10);
export function monday(){const key=dateKey(new Date());return shifted(key,-((new Date(key+'T12:00:00Z').getUTCDay()+6)%7));}
export function feedbackDeadline(training){const key=dateKey(training.end);const end=new Date(training.end);const parts=new Intl.DateTimeFormat('en-US',{timeZone:zone,hourCycle:'h23',hour:'2-digit',minute:'2-digit',second:'2-digit'}).formatToParts(end);const get=t=>Number(parts.find(p=>p.type===t).value);const endSeconds=get('hour')*3600+get('minute')*60+get('second');return new Date(Math.max(end.getTime()+3600000,end.getTime()+(22*3600-endSeconds)*1000));}
