export type FavoriteMode='proxy'|'direct'|'external';
export type FavoriteApp={id:string;name:string;slug:string;enabled:boolean;
  availableToAll:boolean;upstreamUrl?:string;allowedUsers?:string[]};
export type Favorite={id:string;appId:string;name:string;appName:string;
  slug:string;path:string;username:string;autoLogin:boolean;enabled:boolean;
  sortOrder:number;hasPassword:boolean;url?:string;openMode?:FavoriteMode};
export type FavoriteInput={appId:string;name:string;path:string;
  username:string;autoLogin:boolean;enabled:boolean;sortOrder:number;
  password?:string;clearPassword?:boolean};
export type PersonalSiteInput={
  name:string;url:string;username:string;autoLogin:boolean;enabled:boolean;
  sortOrder:number;password?:string;clearPassword?:boolean;openMode?:FavoriteMode;
};
export type AppInput={name:string;slug:string;upstreamUrl:string;
  enabled:boolean;availableToAll:boolean;allowedUsers:string[]};

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
  const result=await fetch(path,{method,credentials:'same-origin',
    cache:'no-store',headers,
    ...(data!==undefined?{body:JSON.stringify(data)}:{})});
  if(!result.ok){
    const details=await result.json().catch(()=>({})) as {error?:string;title?:string};
    throw new Error(details.error||details.title||'Fehler bei Favoriten (HTTP '+result.status+').');
  }
  if(result.status===204)return undefined as T;
  return await result.json() as T;
}
export const loadFavoriteApps=()=>request<FavoriteApp[]>('/api/v1/favorites/apps');
export const loadFavorites=()=>request<Favorite[]>('/api/v1/favorites/');
export const createFavorite=(data:FavoriteInput)=>request<{id:string}>(
  '/api/v1/favorites/','POST',data);
export const updateFavorite=(id:string,data:FavoriteInput)=>request<void>(
  '/api/v1/favorites/'+encodeURIComponent(id),'PUT',data);
export const removeFavorite=(id:string)=>request<void>(
  '/api/v1/favorites/'+encodeURIComponent(id),'DELETE');
export const startFavorite=(id:string)=>request<{url:string}>(
  '/api/v1/favorites/'+encodeURIComponent(id)+'/start','POST',{});
export const loadApprovedApps=()=>request<FavoriteApp[]>('/api/v1/admin/favorite-apps/');
export const createApprovedApp=(data:AppInput)=>request<{id:string}>(
  '/api/v1/admin/favorite-apps/','POST',data);
export const updateApprovedApp=(id:string,data:AppInput)=>request<void>(
  '/api/v1/admin/favorite-apps/'+encodeURIComponent(id),'PUT',data);
export const removeApprovedApp=(id:string)=>request<void>(
  '/api/v1/admin/favorite-apps/'+encodeURIComponent(id),'DELETE');
/** No admin site approvals: each user owns the HTTPS URL and sort order. */
export const createPersonalFavorite=(input:PersonalSiteInput)=>
  request<{id:string}>('/api/v1/favorites/personal','POST',input);
export const updatePersonalFavorite=(id:string,input:PersonalSiteInput)=>
  request<void>('/api/v1/favorites/personal/'+encodeURIComponent(id),'PUT',input);
export const removePersonalFavorite=(id:string)=>
  request<void>('/api/v1/favorites/personal/'+encodeURIComponent(id),'DELETE');
export const sortPersonalFavorites=(ids:string[])=>
  request<void>('/api/v1/favorites/personal/order','PUT',{ids});


export const setFavoriteMode=(id:string,openMode:FavoriteMode)=>request<void>('/api/v1/favorites/personal/'+encodeURIComponent(id)+'/mode','PUT',{openMode});
