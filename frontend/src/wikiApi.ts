export type WikiSettings = {
  configured:boolean;name:string;homePath:string;
  username:string;hasPassword:boolean;ready:boolean;favoriteId:string|null;
};
export type WikiAdminSettings = {
  configured:boolean;name:string;baseUrl:string;homePath:string;
};
export type WikiAdminInput = {name:string;baseUrl:string;homePath:string};

async function request<T>(path:string,method='GET',data?:unknown):Promise<T>{
  const headers:Record<string,string>={};
  if(data!==undefined)headers['Content-Type']='application/json';
  if(method!=='GET'){
    const token=await fetch('/api/v1/auth/csrf',{
      credentials:'same-origin',cache:'no-store'});
    if(!token.ok)throw new Error('Sicherheitstoken konnte nicht geladen werden.');
    const csrf=await token.json() as {token:string};
    headers['X-CSRF-TOKEN']=csrf.token;
  }
  const response=await fetch(path,{
    method,credentials:'same-origin',cache:'no-store',headers,
    ...(data!==undefined?{body:JSON.stringify(data)}:{})
  });
  if(!response.ok){
    const details=await response.json().catch(()=>({})) as {error?:string;detail?:string};
    throw new Error(details.error||details.detail||
      'Wiki-Anfrage fehlgeschlagen (HTTP '+response.status+').');
  }
  if(response.status===204)return undefined as T;
  return await response.json() as T;
}

export const readWikiSettings=()=>request<WikiSettings>('/api/v1/wiki/settings');
export const saveWikiLogin=(username:string,password:string)=>
  request<void>('/api/v1/wiki/settings','PUT',{username,password:password||null});
export const deleteWikiLogin=()=>request<void>('/api/v1/wiki/settings','DELETE');
export const readWikiAdmin=()=>request<WikiAdminSettings>('/api/v1/wiki/admin/');
export const saveWikiAdmin=(data:WikiAdminInput)=>
  request<void>('/api/v1/wiki/admin/','PUT',data);
export const startWiki=()=>request<{url:string}>('/api/v1/wiki/start','POST',{});

// Only the backend-issued single-use proxy bootstrap may become iframe src.
export function validWikiBootstrap(raw:string):boolean{
  try{
    const url=new URL(raw);
    return url.protocol==='https:'&&!url.username&&!url.password&&
      url.origin!==window.location.origin&&
      (url.pathname==='/_plenaro/favorite-bootstrap'||
       url.pathname==='/wiki/_plenaro/favorite-bootstrap');
  }catch{return false;}
}
