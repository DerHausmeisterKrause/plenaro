import {useEffect,useState} from 'react';
import {useLocation} from 'react-router-dom';
import {currentUser} from './authApi';
import {FavoritePage} from './FavoritesPage';
import {WikiPage} from './WikiPage';
import {ZnunyWebPortalPage} from './ZnunyWebPortalPage';

type Page={key:string;kind:'wiki'|'ticket'|'favorite';id?:string;ticket?:string|null;requestKey?:string};
const MAX_FAVORITES=8;

function activePage(path:string,search:string,key:string):Page|null{
  if(path==='/wiki')return {key:'wiki',kind:'wiki'};
  if(path==='/ticket-system'){
    const ticket=new URLSearchParams(search).get('ticket');
    return {key:'ticket',kind:'ticket',ticket,requestKey:ticket===null?undefined:key};
  }
  const match=/^\/favorites\/([^/]+)$/.exec(path);
  if(!match)return null;
  try{
    const id=decodeURIComponent(match[1]);
    return {key:'favorite:'+id,kind:'favorite',id};
  }catch{return null;}
}

// A stable DOM parent for each visited website, just like the Classic WPF
// persistent WebView host. Do not reparent iframes or replay bootstrap URLs.
export function RetainedWebWorkspace({userId,admin,onInvalidSession}:{
  userId:string;admin:boolean;onInvalidSession:()=>void;
}){
  const location=useLocation();
  const selected=activePage(location.pathname,location.search,location.key);
  const [pages,setPages]=useState<Page[]>([]);
  const [verified,setVerified]=useState('');
  const [error,setError]=useState('');
  const [retry,setRetry]=useState(0);
  const activeKey=selected?.key??'';
  const visitKey=activeKey?location.key+':'+activeKey+':'+retry:'';
  const settings=location.pathname==='/settings';
  // Settings can change URLs, credentials or proxy policy; close all old
  // frames before editing instead of retaining a stale privileged session.
  useEffect(()=>{
    if(settings){setPages([]);setVerified('');}
  },[settings]);

  useEffect(()=>{
    if(!activeKey){setVerified('');setError('');return;}
    let live=true;
    setError('');
    void currentUser().then(user=>{
      if(!live)return;
      if(!user||user.id!==userId){
        setPages([]);setVerified('');onInvalidSession();return;
      }
      setVerified(visitKey);
    }).catch(()=>{
      if(live){setVerified('');setError('Sitzung konnte nicht geprüft werden.');}
    });
    return()=>{live=false;};
  },[visitKey,activeKey,userId,onInvalidSession]);

  const ready=!!selected&&verified===visitKey;
  // Keep each retained component at its original keyed DOM position. Only
  // the oldest opened favorite is evicted when the limit is exceeded.
  const next=ready?remember(pages,selected!):pages;
  useEffect(()=>{
    if(ready)setPages(old=>remember(old,selected!));
  },[ready,activeKey,selected?.requestKey]);
  return <>
    {selected&&!ready&&<div className="page">
      {error?<div role="alert">{error} <button type="button" className="button button-secondary"
        onClick={()=>setRetry(n=>n+1)}>Erneut versuchen</button></div>:
        <p role="status">Sitzung wird geprüft …</p>}
    </div>}
    {!settings&&next.map(page=>{
      const visible=ready&&page.key===activeKey;
      return <div key={page.key} className="retained-web-page" hidden={!visible} inert={!visible}>
        {page.kind==='wiki'?<WikiPage/>:page.kind==='ticket'?
          <ZnunyWebPortalPage retainedTicket={page.ticket??null} navigationKey={page.requestKey}/>:
          <FavoritePage favoriteId={page.id} admin={admin} visible={visible}/>}
      </div>;
    })}
  </>;
}

export function remember(pages:Page[],selected:Page):Page[]{
  const previous=pages.find(p=>p.key===selected.key);
  // The plain sidebar link resumes the last native ticket page. Only an
  // explicit ?ticket= navigation requests a new destination.
  const page=previous&&selected.kind==='ticket'&&!selected.requestKey?previous:selected;
  // Keep physical DOM order: moving iframe nodes can reload them. Retain
  // creation order and evict the oldest opened favorite (FIFO).
  if(previous){
    if(previous===page||previous.requestKey===page.requestKey)return pages;
    return pages.map(p=>p.key===page.key?page:p);
  }
  const favorites=pages.filter(p=>p.kind==='favorite');
  const retained=page.kind==='favorite'&&favorites.length>=MAX_FAVORITES?
    pages.filter(p=>p.key!==favorites[0].key):pages;
  return [...retained,page];
}
