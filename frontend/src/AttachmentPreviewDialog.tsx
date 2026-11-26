import {lazy,Suspense,useEffect,useRef,useState} from 'react';
import {createPortal} from 'react-dom';
import {Download,ShieldCheck,X} from 'lucide-react';
import type {ZnunyAttachment} from './znunySyncApi';

const PdfPage=lazy(()=>import('./AttachmentPdfPage'));
const allowed=['image/png','image/jpeg','image/gif','image/webp','application/pdf'];
const maxBytes=5*1024*1024;
export function canPreviewAttachment(file:ZnunyAttachment){
  const mime=(file.contentType??'').split(';')[0].trim().toLowerCase();
  return allowed.includes(mime)||((!mime||mime==='application/octet-stream')&&
    /\.(?:png|jpe?g|gif|webp|pdf)$/i.test(file.filename??''));
}
type Loaded={file:Blob;url:string;mime:string};

export function AttachmentPreviewDialog({endpoint,attachment,onClose}:{
  endpoint:string;attachment:ZnunyAttachment;onClose:()=>void;
}){
  const dialog=useRef<HTMLDialogElement>(null);
  const closeButton=useRef<HTMLButtonElement>(null);
  const [loaded,setLoaded]=useState<Loaded|null>(null);
  const [error,setError]=useState('');
  const [attempt,setAttempt]=useState(0);
  useEffect(()=>{
    const element=dialog.current;
    if(!element)return;
    const previous=document.activeElement;
    if(typeof element.showModal==='function')element.showModal();
    else element.setAttribute('open','');
    closeButton.current?.focus();
    return()=>{
      if(typeof element.close==='function')element.close();
      else element.removeAttribute('open');
      if(previous instanceof HTMLElement&&previous.isConnected)previous.focus();
    };
  },[]);
  useEffect(()=>{
    const controller=new AbortController();
    let active=true;
    let url:string|undefined;
    setLoaded(null);setError('');
    void(async()=>{
      try{
        const result=await fetch(endpoint,{credentials:'same-origin',cache:'no-store',
          redirect:'error',signal:controller.signal});
        if(!active)return;
        if(!result.ok){
          const body=await result.json().catch(()=>({})) as {error?:string};
          throw new Error(body.error||'Vorschau nicht verfügbar (HTTP '+result.status+').');
        }
        const mime=(result.headers.get('Content-Type')??'').split(';')[0].trim().toLowerCase();
        if(!allowed.includes(mime))throw new Error('Unerwarteter Dateityp.');
        const bytes=await result.blob();
        if(!active)return;
        if(bytes.size===0||bytes.size>maxBytes)throw new Error('Ungültige Dateigröße.');
        const file=new Blob([bytes],{type:mime});
        url=URL.createObjectURL(file);
        setLoaded({file,url,mime});
      }catch(e){
        if(active&&!controller.signal.aborted)
          setError(e instanceof Error?e.message:'Anhang konnte nicht geprüft werden.');
      }
    })();
    return()=>{active=false;controller.abort();if(url)URL.revokeObjectURL(url);};
  },[endpoint,attempt]);

  // Keep the clean server cache alive only while a preview is open.
  // The endpoint returns no attachment bytes and checks access again.
  useEffect(()=>{
    if(!loaded)return;
    const controller=new AbortController();
    let active=true;
    const interval=window.setInterval(()=>{
      void fetch(endpoint+'/keepalive',{credentials:'same-origin',cache:'no-store',
        redirect:'error',signal:controller.signal})
        .then(response=>{
          if(active&&(response.status===401||response.status===403)){
            setLoaded(null);setError('Zugriff auf die Datei wurde beendet.');
            URL.revokeObjectURL(loaded.url);
          }
        }).catch(()=>{});
    },30_000);
    return()=>{active=false;window.clearInterval(interval);controller.abort();};
  },[endpoint,loaded]);

  return createPortal(<dialog ref={dialog} className="attachment-preview-dialog"
    aria-label="Anhang-Vorschau" onCancel={e=>{e.preventDefault();onClose();}}>
    <div className="attachment-preview-heading">
      <strong title={attachment.filename}>{attachment.filename||'Anhang'}</strong>
      <button ref={closeButton} type="button" className="icon-button"
        aria-label="Vorschau schließen" onClick={onClose}><X size={21}/></button>
    </div>
    {!loaded&&!error&&<div className="attachment-preview-status" role="status">
      <progress aria-label="Anhang wird geladen und geprüft"/>
      <strong>Anhang wird geladen und auf Schadsoftware geprüft …</strong>
      <span>Die Anzeige wird erst nach erfolgreicher Prüfung freigegeben.</span>
    </div>}
    {error&&<div className="attachment-preview-status" role="alert">
      <strong>Vorschau nicht verfügbar</strong><p>{error}</p>
      <button type="button" className="button button-secondary"
        onClick={()=>setAttempt(a=>a+1)}>Erneut versuchen</button>
    </div>}
    {loaded&&<div className="attachment-preview-content">
      <div className="attachment-preview-verified">
        <ShieldCheck size={17}/><span>ClamAV-Prüfung ohne Fund</span>
        <a className="classic-text-action" href={loaded.url} download={attachment.filename||'Anhang'}>
          <Download size={15}/>Geprüfte Datei herunterladen
        </a>
      </div>
      {loaded.mime==='application/pdf'?
        <Suspense fallback={<div role="status">PDF-Viewer wird geladen …</div>}>
          <PdfPage file={loaded.file}/>
        </Suspense>:
        <div className="attachment-preview-image-wrap">
          <img className="attachment-preview-image" src={loaded.url}
            alt={attachment.filename||'Anhang'}
            onError={()=>{setLoaded(null);setError('Bild kann nicht dargestellt werden.');URL.revokeObjectURL(loaded.url);}}/>
        </div>}
    </div>}
    <div className="attachment-preview-footer">Temporäre Vorschau · Cache verfällt nach drei Minuten ohne Zugriff.</div>
  </dialog>,document.body);
}
