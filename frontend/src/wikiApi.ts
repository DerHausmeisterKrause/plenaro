export type WikiSelection = {favoriteId:string|null};

export async function readWikiSelection():Promise<WikiSelection>{
  const response=await fetch('/api/v1/wiki/settings',{
    credentials:'same-origin',cache:'no-store'});
  if(!response.ok)throw new Error('Wiki-Einstellungen konnten nicht geladen werden.');
  return await response.json() as WikiSelection;
}

export async function saveWikiSelection(favoriteId:string|null):Promise<void>{
  const csrfResponse=await fetch('/api/v1/auth/csrf',{
    credentials:'same-origin',cache:'no-store'});
  if(!csrfResponse.ok)throw new Error('Sicherheitstoken konnte nicht geladen werden.');
  const csrf=await csrfResponse.json() as {token:string};
  const response=await fetch('/api/v1/wiki/settings',{
    method:'PUT',credentials:'same-origin',cache:'no-store',
    headers:{'Content-Type':'application/json','X-CSRF-TOKEN':csrf.token},
    body:JSON.stringify({favoriteId})
  });
  if(!response.ok){
    const details=await response.json().catch(()=>({})) as {error?:string};
    throw new Error(details.error||'Wiki-Quelle konnte nicht gespeichert werden.');
  }
}

// The API issues single-use bootstrap URLs only for the currently approved
// favorite. Never embed an arbitrary user-entered Wiki URL in an iframe.
export function validWikiBootstrap(raw:string,slug:string):boolean{
  try{
    const url=new URL(raw);
    const legacy=url.pathname==='/_plenaro/favorite-bootstrap';
    const shared=url.pathname==='/'+slug+'/_plenaro/favorite-bootstrap';
    return url.protocol==='https:' && !url.username && !url.password &&
      url.origin!==window.location.origin && (legacy||shared);
  }catch{return false;}
}
