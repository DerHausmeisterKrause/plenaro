export type BrRecipients = {recipient1:string;recipient2:string};
export type BrMailStatus = {status:'none'|'processing'|'accepted'|'unknown'};
export type BrSendResult = {status:string;message:string};

async function request<T>(path:string,method='GET',data?:unknown):Promise<T>{
  const headers:Record<string,string>={};
  if(data!==undefined)headers['Content-Type']='application/json';
  if(method!=='GET'){
    const csrf=await fetch('/api/v1/auth/csrf',{
      cache:'no-store',credentials:'same-origin'
    });
    if(!csrf.ok)throw new Error('Sicherheitstoken konnte nicht geladen werden.');
    const token=await csrf.json() as {token:string};
    headers['X-CSRF-TOKEN']=token.token;
  }
  const response=await fetch(path,{
    method,credentials:'same-origin',cache:'no-store',headers,
    ...(data!==undefined?{body:JSON.stringify(data)}:{})
  });
  if(!response.ok){
    const detail=await response.json().catch(()=>({})) as {
      error?:string;detail?:string
    };
    throw new Error(detail.error||detail.detail||
      'Bereitschaftsantrag fehlgeschlagen (HTTP '+response.status+').');
  }
  if(response.status===204)return undefined as T;
  return await response.json() as T;
}

export const readBrRecipients=()=>request<BrRecipients>('/api/v1/br-requests/settings');
export const saveBrRecipients=(data:BrRecipients)=>
  request<void>('/api/v1/br-requests/settings','PUT',data);
export const readBrMailStatus=(day:string)=>
  request<BrMailStatus>('/api/v1/br-requests/'+encodeURIComponent(day)+'/status');
export const preflightHomeofficeMail=()=>
  request<{ready:boolean}>('/api/v1/br-requests/preflight','POST',{});
export const sendBrRequest=(day:string)=>
  request<BrSendResult>('/api/v1/br-requests/'+encodeURIComponent(day)+'/send','POST',{});
export function GermanDay(day:string):string{
  const parts=day.split('-');
  if(parts.length!==3)return day;
  return parts[2]+'.'+parts[1]+'.'+parts[0];
}
