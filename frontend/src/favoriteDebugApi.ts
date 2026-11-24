export type FavoriteDebugEntry={
  id:number;at:string;level:'info'|'warning'|'error';code:string;
  method:string;resource:string;status:number|null;
};
export type FavoriteDebugBatch={enabled:boolean;events:FavoriteDebugEntry[]};

async function debugRequest<T>(path:string,method='GET',body?:unknown):Promise<T>{
  const headers:Record<string,string>={};
  if(method!=='GET'){
    const csrf=await fetch('/api/v1/auth/csrf',{
      credentials:'same-origin',cache:'no-store'
    });
    if(!csrf.ok)throw new Error('Sicherheitstoken konnte nicht geladen werden.');
    const result=await csrf.json() as {token:string};
    headers['X-CSRF-TOKEN']=result.token;
  }
  if(body!==undefined)headers['Content-Type']='application/json';
  const response=await fetch('/api/v1/admin/favorites-debug'+path,{
    method,credentials:'same-origin',cache:'no-store',headers,
    ...(body!==undefined?{body:JSON.stringify(body)}:{})
  });
  if(!response.ok){
    if(response.status===403)return Promise.reject(
      new Error('Nur Administratoren dürfen die Favoriten-Diagnose verwenden.'));
    throw new Error('Favoriten-Diagnose fehlgeschlagen (HTTP '+response.status+').');
  }
  if(response.status===204)return undefined as T;
  return await response.json() as T;
}
export const readFavoriteDebugSetting=()=>debugRequest<{enabled:boolean}>('/settings');
export const setFavoriteDebugSetting=(enabled:boolean)=>
  debugRequest<void>('/settings','PUT',{enabled});
export const readFavoriteDebugEvents=(favoriteId:string,after=0)=>
  debugRequest<FavoriteDebugBatch>('/events?'+
    new URLSearchParams({favoriteId,after:String(after)}).toString());
export const clearFavoriteDebugEvents=(favoriteId:string)=>
  debugRequest<void>('/clear','POST',{favoriteId});
