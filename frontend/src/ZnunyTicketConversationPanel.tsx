import {useEffect,useState,type FormEvent} from 'react';
import {Download,Eye,FileText,Maximize2,Minimize2,Paperclip,RefreshCw,Send} from 'lucide-react';
import {AttachmentPreviewDialog,canPreviewAttachment} from './AttachmentPreviewDialog';
import {emailFrameDocument,isHtmlMail,normalizeCid,referencedCids} from './emailBody';
import {getZnunyPersonal,getZnunyServer} from './znunyApi';
import {customerArticle,ensureCloseNotice,renderReplyTemplate} from './znunyReplyTemplate';
import {
  readZnunyArticles,readZnunyInlineImage,sendZnunyReply,markZnunyArticleRead,
  znunyAttachmentDownloadUrl,type ZnunyArticle,type ZnunyAttachment,
} from './znunySyncApi';

// Znuny timestamps are normally ISO or YYYY-MM-DD HH:mm:ss. Compare
// calendar timestamps consistently; numeric article IDs break ties.
function newestFirst(articles:ZnunyArticle[]){
  const time=(value:string)=>Date.parse(value.replace(' ','T'));
  return [...articles].sort((a,b)=>{
    const ta=time(a.created),tb=time(b.created);
    if(Number.isFinite(ta)&&Number.isFinite(tb)&&ta!==tb)return tb-ta;
    if(/^\d+$/.test(a.id)&&/^\d+$/.test(b.id))
      return b.id.length-a.id.length||b.id.localeCompare(a.id);
    return b.id.localeCompare(a.id);
  });
}
// Keep unzoned Znuny timestamps as supplied (no implicit UTC shift).
function germanArticleDate(value:string):string {
  const match=value?.trim().match(/^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})(?::(\d{2}))?(?:\.\d+)?$/);
  if(match)return match[3]+'.'+match[2]+'.'+match[1]+' '+match[4]+':'+match[5]+':'+(match[6]??'00');
  const date=new Date(value);
  if(!value||!Number.isFinite(date.getTime()))return value||'Datum unbekannt';
  const datePart=new Intl.DateTimeFormat('de-DE',{day:'2-digit',month:'2-digit',year:'numeric'}).format(date);
  const timePart=new Intl.DateTimeFormat('de-DE',{hour:'2-digit',minute:'2-digit',second:'2-digit',hourCycle:'h23'}).format(date);
  return datePart+' '+timePart;
}
function selectedArticleId(articles:ZnunyArticle[],unread:string[],previous=''){
  if(articles.some(a=>a.id===previous))return previous;
  const ordered=newestFirst(articles);
  return ordered.find(a=>unread.includes(a.id))?.id??ordered[0]?.id??'';
}

export function ZnunyTicketConversationPanel({ticketId,closed,closeRequest=0,onClosed,onReadChange}:{
  ticketId:string;closed:boolean;closeRequest?:number;
  onClosed?:()=>void;onReadChange?:()=>void;
}) {
  const [opened,setOpened]=useState(true);
  const [preview,setPreview]=useState<{ticket:string;article:string;file:ZnunyAttachment}|null>(null);
  const [articles,setArticles]=useState<ZnunyArticle[]>([]);
  const [ticketNumber,setTicketNumber]=useState('');
  const [ticketTitle,setTicketTitle]=useState('');
  const [selectedId,setSelectedId]=useState('');
  const [unreadIds,setUnreadIds]=useState<string[]>([]);
  const [busy,setBusy]=useState(false);
  const [replying,setReplying]=useState(false);
  const [closeMode,setCloseMode]=useState(false);
  const [templateWarning,setTemplateWarning]=useState('');
  const [emailExpanded,setEmailExpanded]=useState(false);
  const [cidImages,setCidImages]=useState<Record<string,string>>({});
  const [reply,setReply]=useState('');
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [loaded,setLoaded]=useState(false);
  const [fetchedAt,setFetchedAt]=useState('');
  const [stale,setStale]=useState(false);
  async function reload(refresh=false){
    setBusy(true);setError('');
    try{
      const data=await readZnunyArticles(ticketId,refresh);
      setFetchedAt(data.fetchedAt??'');setStale(data.stale===true);
      setArticles(data.articles);
      setUnreadIds(data.unreadArticleIds??[]);
      setTicketNumber(data.ticketNumber);setTicketTitle(data.title);
      setSelectedId(id=>selectedArticleId(data.articles,data.unreadArticleIds??[],id));
      setLoaded(true);
    }catch(e){
      setError(e instanceof Error?e.message:'Ticket-Nachrichten nicht verfügbar.');
    }finally{setBusy(false);}
  }
  useEffect(()=>{
    setPreview(null);setArticles([]);setSelectedId('');setUnreadIds([]);setOpened(true);setEmailExpanded(false);
    setTicketNumber('');setTicketTitle('');
    setLoaded(false);setFetchedAt('');setStale(false);setError('');setNotice('');setReplying(false);setReply('');
    setCloseMode(false);setTemplateWarning('');
    void reload();
  },[ticketId]);
  // Keep newly arriving Znuny messages visible without a manual refresh. Do
  // not interrupt an unsent reply and never clear already displayed content
  // on a transient network failure.
  useEffect(()=>{
    const interval=window.setInterval(()=>{
      if(document.hidden||busy||replying)return;
      void readZnunyArticles(ticketId).then(data=>{
        setFetchedAt(data.fetchedAt??'');setStale(data.stale===true);
        setArticles(data.articles);
        setUnreadIds(data.unreadArticleIds??[]);
        setSelectedId(id=>selectedArticleId(data.articles,data.unreadArticleIds??[],id));
      }).catch(()=>{ /* existing articles remain readable */ });
    },60_000);
    return()=>window.clearInterval(interval);
  },[ticketId,busy,replying]);
  // Like WPF: reading the selected article (not merely opening the ticket)
  // acknowledges exactly that article. Other unread articles keep the star.
  useEffect(()=>{
    if(!loaded||!opened||!selectedId||!unreadIds.includes(selectedId))return;
    let active=true;
    void markZnunyArticleRead(ticketId,selectedId).then(()=>{
      if(!active)return;
      setUnreadIds(old=>old.filter(id=>id!==selectedId));
      onReadChange?.();
    }).catch(()=>{ /* Keep unread if the server could not confirm reading. */ });
    return()=>{active=false;};
  },[ticketId,selectedId,unreadIds,loaded,opened,onReadChange]);

  const selected=articles.find(a=>a.id===selectedId);
  const htmlMail=selected ? isHtmlMail(selected.body,selected.contentType) : false;
  useEffect(()=>{
    setCidImages({});
    if(!selected||!htmlMail)return;
    const images=selected.attachments??[];
    const refs=referencedCids(selected.body);
    // Only image Content-ID values referenced by the chosen article are loaded.
    const matches=refs.slice(0,8).map(cid=>images.find(image=>
      normalizeCid(image.contentId)===cid &&
      /^image\/(png|jpeg|gif|webp)(?:\s*;|$)/i.test(image.contentType) &&
      image.size<=512*1024 && /^\d+$/.test(image.id)))
      .filter((image):image is NonNullable<typeof image>=>Boolean(image));
    let active=true;
    void Promise.allSettled(matches.map(async image=>{
      const result=await readZnunyInlineImage(ticketId,selected.id,image.id);
      return [normalizeCid(image.contentId),result.dataUri] as const;
    })).then(results=>{
      if(!active)return;
      const resolved:Record<string,string>={};
      for(const result of results)
        if(result.status==='fulfilled')
          resolved[result.value[0]]=result.value[1];
      setCidImages(resolved);
    });
    return ()=>{active=false;};
  },[ticketId,selectedId,articles,htmlMail]);
  async function beginReply(close:boolean){
    if(busy||closed)return;
    // The backend validates the actual recipient against Znuny. Do not
    // prevent composing merely because TicketGet omits SenderType/Channel,
    // or the customer address is available only via CustomerUserID.
    const original=customerArticle(articles);
    setBusy(true);setError('');
    try{
      const [server,personal]=await Promise.all([
        getZnunyServer(),getZnunyPersonal()
      ]);
      // Each user can override the admin default; an empty personal template
      // intentionally falls back to the existing global Classic header.
      const template=personal.replyTemplate?.trim()?
        personal.replyTemplate:server.ticketReplyTemplate;
      const rendered=renderReplyTemplate(template??'',original,
        ticketNumber||'',ticketTitle||'');
      const text=close?ensureCloseNotice(rendered.text):rendered.text;
      setReply(text);
      setTemplateWarning(!original?
        'Kein Kundenartikel erkennbar: Der Empfänger wird beim Senden serverseitig aus Znuny (ggf. CustomerUserID) geprüft. Bitte die Antwortvorlage kontrollieren.':
        rendered.unresolved?
        'Einige Vorlagenvariablen konnten nicht aufgelöst werden. Antwort vor dem Senden prüfen.':'');
      setCloseMode(close);setReplying(true);
    }catch(e){
      setError(e instanceof Error?e.message:'Antwortvorlage nicht verfügbar.');
    }finally{setBusy(false);}
  }
  useEffect(()=>{
    if(closeRequest>0)void beginReply(true);
  },[closeRequest]);
  async function submit(e:FormEvent){
    e.preventDefault();
    if(busy||!reply.trim())return;
    if(!window.confirm('Diese Antwort wird als E-Mail über Znuny an den Kunden versendet. Fortfahren?'))return;
    setBusy(true);setError('');setNotice('');
    try{
      await sendZnunyReply(ticketId,reply.trim(),closeMode);
      setReply('');setReplying(false);setTemplateWarning('');
      setNotice(closeMode?'Znuny hat die Antwort und das Schließen bestätigt.':
        'Znuny hat die Antwort bestätigt.');
      if(closeMode)onClosed?.();
      // A failed reload must never invalidate a successfully confirmed write.
      try{
        const data=await readZnunyArticles(ticketId,true);
        setFetchedAt(data.fetchedAt??'');setStale(data.stale===true);
        setArticles(data.articles);
        setUnreadIds(data.unreadArticleIds??[]);
        setSelectedId(newestFirst(data.articles)[0]?.id??'');
      }catch{
        setNotice('Antwort bestätigt. Der Nachrichtenverlauf konnte nicht aktualisiert werden.');
      }
    }catch(err){
      setError(err instanceof Error?err.message:'Antwort nicht bestätigt.');
    }finally{setBusy(false);}
  }
  return <details className="classic-wpf-optional" open={opened}
    onToggle={e=>{
      const next=(e.currentTarget as HTMLDetailsElement).open;
      setOpened(next);
      if(next&&!loaded&&!busy)void reload();
    }}>
    <summary><FileText size={16}/>Ticket-Nachrichten</summary>
    <div className="classic-wpf-optional-inner">
      <div className="classic-wpf-actions">
        <button type="button" className="button button-secondary" disabled={busy}
          onClick={()=>void reload(true)}><RefreshCw size={14}/>Nachrichten aktualisieren</button>
      </div>
      {busy&&<p role="status">Kommunikation mit Znuny …</p>}
      {error&&<p role="alert" className="um-feedback failure">{error}</p>}
      {notice&&<p role="status" className="um-feedback success">{notice}</p>}
      {fetchedAt&&<p className="classic-wpf-help">{stale?'Zwischengespeicherter Stand · ':'Letzter Znuny-Abgleich · '}{germanArticleDate(fetchedAt)}{stale?' · Aktualisierung ausstehend':''}</p>}
      {loaded&&articles.length===0&&<p className="classic-wpf-help">
        Keine Artikel vorhanden oder von der API zurückgegeben.</p>}
      {articles.length>0&&<label>Nachricht
        <select aria-label="Ticket-Nachricht auswählen" className="znuny-message-listbox"
          size={4} value={selectedId}
          onChange={e=>{setPreview(null);setSelectedId(e.target.value);setReplying(false);setEmailExpanded(false);}}>
          {newestFirst(articles).map((a,i)=><option key={a.id||i} value={a.id}>
            {articles.length-i} | {germanArticleDate(a.created)} | {unreadIds.includes(a.id)?'★ Neu · ':''}{a.subject||'Ohne Betreff'}
          </option>)}
        </select>
      </label>}
      {selected&&<article className="znuny-conversation-article">
        <div className="znuny-message-headers">
          <p><strong>Betreff:</strong> {selected.subject||'Ohne Betreff'}</p>
          <p><strong>Von:</strong> {selected.from||'Unbekannt'}</p>
          {selected.to&&<p><strong>An:</strong> {selected.to}</p>}
          <p><strong>Datum:</strong> {germanArticleDate(selected.created)} · {selected.channel||selected.senderType}</p>
        </div>
        {htmlMail&&selected.body&&<>
          <div className="znuny-message-toolbar">
            <span className="classic-wpf-help">HTML-E-Mail · Sichere Vorschau ohne externe Bilder und aktive Inhalte</span>
            <button type="button" className="button button-secondary"
              onClick={()=>setEmailExpanded(expanded=>!expanded)}>
              {emailExpanded?<Minimize2 size={14}/>:<Maximize2 size={14}/>}
              {emailExpanded?'Ansicht verkleinern':'Ansicht vergrößern'}
            </button>
          </div>
          <iframe key={selected.id} title="HTML-E-Mail-Inhalt" className={
            'znuny-email-frame'+(emailExpanded?' znuny-email-frame-expanded':'')}
            sandbox="" referrerPolicy="no-referrer" loading="lazy"
            srcDoc={emailFrameDocument(selected.body,cidImages)}/>
        </>}
        {!htmlMail&&<div className="znuny-conversation-body" aria-label="Nachrichtentext">
          {selected.body||'Kein Nachrichtentext'}
        </div>}
        {(selected.attachments?.length??0)>0&&<section className="znuny-attachments"
          aria-label="E-Mail-Anhänge">
          <h4><Paperclip size={15}/>Anhänge ({selected.attachments?.length})</h4>
          <div className="znuny-attachment-list">
            {selected.attachments?.map(file=>{
              const label=<>
                <span className="znuny-attachment-name">{file.filename||'Anhang '+file.id}</span>
                {file.size>0&&<span className="znuny-attachment-size">
                  {(file.size/1024).toLocaleString('de-DE',{maximumFractionDigits:1})} KB
                </span>}
              </>;
              return canPreviewAttachment(file)?
                <button type="button" key={file.id} className="znuny-attachment-link znuny-attachment-preview"
                  title="Geprüfte Vorschau öffnen"
                  onClick={()=>setPreview({ticket:ticketId,article:selected.id,file})}>
                  <Eye size={15}/>{label}
                </button>:
                <a key={file.id} href={znunyAttachmentDownloadUrl(ticketId,selected.id,file.id)}
                  className="znuny-attachment-link" title="Original herunterladen (kein Vorschau-Virenscan)">
                  <Download size={15}/>{label}
                </a>;
            })}
          </div>
          <p className="classic-wpf-help">
            PDF- und Bilddateien öffnen sich nach erfolgreicher ClamAV-Prüfung in einer
            Vorschau. Andere Dateitypen bleiben als Original-Download verfügbar (ohne
            Vorschau-Virenscan). Eingebettete kleine E-Mail-Bilder bleiben unverändert.
          </p>
        </section>}
        {!closed&&!replying&&<button type="button" className="button button-secondary"
          onClick={()=>void beginReply(false)}>Antworten</button>}
      </article>}
      {!closed&&loaded&&articles.length===0&&!replying&&
        <button type="button" className="button button-secondary"
          onClick={()=>void beginReply(false)}>Antworten</button>}
      {replying&&!closed&&<form className="um-form" onSubmit={e=>void submit(e)}>
        <h4>{closeMode?'Antwort senden und Ticket schließen':'Antwort schreiben'}</h4>
        {templateWarning&&<p role="alert" className="um-feedback failure">{templateWarning}</p>}
        <p className="classic-wpf-help">Der Empfänger wird beim Senden vom Server
          aus einem Znuny-Kundenartikel oder der gültigen CustomerUserID ermittelt.
          Frei eingegebene Empfänger werden nicht akzeptiert. Die Nachricht
          wird unmittelbar als E-Mail versendet.</p>
        <label htmlFor="znuny-reply-body">Antworttext</label>
        <textarea id="znuny-reply-body" rows={8} maxLength={10000}
          required value={reply} onChange={e=>setReply(e.target.value)}/>
        <div className="classic-wpf-actions">
          <button type="submit" className="button button-primary"
            disabled={busy||!reply.trim()}><Send size={14}/>
            {closeMode?'Antwort senden & schließen':'Antwort senden'}</button>
          <button type="button" className="button button-secondary"
            disabled={busy} onClick={()=>{setReplying(false);setCloseMode(false);
              setTemplateWarning('');setReply('');}}>Abbrechen</button>
        </div>
      </form>}
      {!closed&&loaded&&articles.length===0&&<p className="classic-wpf-help">
        Keine Kundenartikel sichtbar. Beim Antworten versucht Plenaro zusätzlich,
        die gültige E-Mail-Adresse aus Znunys CustomerUserID zu verwenden.</p>}
      {preview&&preview.ticket===ticketId&&preview.article===selected?.id&&
        <AttachmentPreviewDialog key={preview.ticket+':'+preview.article+':'+preview.file.id}
          endpoint={znunyAttachmentDownloadUrl(preview.ticket,preview.article,preview.file.id)+'/preview'}
          attachment={preview.file} onClose={()=>setPreview(null)}/>}
    </div>
  </details>;
}
