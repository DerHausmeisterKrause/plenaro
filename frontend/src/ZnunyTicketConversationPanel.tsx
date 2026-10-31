import {useEffect,useState,type FormEvent} from 'react';
import {FileText,RefreshCw,Send} from 'lucide-react';
import {
  readZnunyArticles,sendZnunyReply,type ZnunyArticle,
} from './znunySyncApi';

export function ZnunyTicketConversationPanel({ticketId,closed}:{
  ticketId:string;closed:boolean;
}) {
  const [opened,setOpened]=useState(true);
  const [articles,setArticles]=useState<ZnunyArticle[]>([]);
  const [selectedId,setSelectedId]=useState('');
  const [busy,setBusy]=useState(false);
  const [replying,setReplying]=useState(false);
  const [reply,setReply]=useState('');
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [loaded,setLoaded]=useState(false);
  async function reload(){
    setBusy(true);setError('');
    try{
      const data=await readZnunyArticles(ticketId);
      setArticles(data.articles);
      setSelectedId(id=>data.articles.some(item=>item.id===id)?id:
        data.articles[data.articles.length-1]?.id??'');
      setLoaded(true);
    }catch(e){
      setError(e instanceof Error?e.message:'Ticket-Nachrichten nicht verfügbar.');
    }finally{setBusy(false);}
  }
  useEffect(()=>{
    setArticles([]);setSelectedId('');setOpened(true);
    setLoaded(false);setError('');setNotice('');setReplying(false);setReply('');
    void reload();
  },[ticketId]);
  const selected=articles.find(a=>a.id===selectedId);
  async function submit(e:FormEvent){
    e.preventDefault();
    if(busy||!reply.trim())return;
    if(!window.confirm('Diese Antwort wird als E-Mail über Znuny an den Kunden versendet. Fortfahren?'))return;
    setBusy(true);setError('');setNotice('');
    try{
      await sendZnunyReply(ticketId,reply.trim());
      setReply('');setReplying(false);
      setNotice('Znuny hat die Antwort bestätigt.');
      // A failed reload must never invalidate a successfully confirmed write.
      try{
        const data=await readZnunyArticles(ticketId);
        setArticles(data.articles);
        setSelectedId(data.articles[data.articles.length-1]?.id??'');
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
          onClick={()=>void reload()}><RefreshCw size={14}/>Nachrichten aktualisieren</button>
      </div>
      {busy&&<p role="status">Kommunikation mit Znuny …</p>}
      {error&&<p role="alert" className="um-feedback failure">{error}</p>}
      {notice&&<p role="status" className="um-feedback success">{notice}</p>}
      {loaded&&articles.length===0&&<p className="classic-wpf-help">
        Keine Artikel vorhanden oder von der API zurückgegeben.</p>}
      {articles.length>0&&<label>Nachricht
        <select aria-label="Ticket-Nachricht auswählen" value={selectedId}
          onChange={e=>{setSelectedId(e.target.value);setReplying(false);}}>
          {articles.map((a,i)=><option key={a.id||i} value={a.id}>
            {i+1} · {a.created||'Datum unbekannt'} · {a.subject||'Ohne Betreff'}
          </option>)}
        </select>
      </label>}
      {selected&&<article className="znuny-conversation-article">
        <p><strong>Von:</strong> {selected.from||'Unbekannt'}</p>
        <p><strong>Datum:</strong> {selected.created||'Unbekannt'} · {selected.channel||selected.senderType}</p>
        <div className="znuny-conversation-body">{selected.body||'Kein Nachrichtentext'}</div>
        {!closed&&!replying&&<button type="button" className="button button-secondary"
          onClick={()=>setReplying(true)}>Antworten</button>}
      </article>}
      {replying&&!closed&&<form className="um-form" onSubmit={e=>void submit(e)}>
        <h4>Antwort schreiben</h4>
        <p className="classic-wpf-help">Empfänger wird ausschließlich aus der letzten
          Kunden-E-Mail in Znuny ermittelt, nicht aus frei eingegebenen Adressen.
          Die Nachricht wird unmittelbar als E-Mail versendet.</p>
        <label htmlFor="znuny-reply-body">Antworttext</label>
        <textarea id="znuny-reply-body" rows={8} maxLength={10000}
          required value={reply} onChange={e=>setReply(e.target.value)}/>
        <div className="classic-wpf-actions">
          <button type="submit" className="button button-primary"
            disabled={busy||!reply.trim()}><Send size={14}/>Antwort senden</button>
          <button type="button" className="button button-secondary"
            disabled={busy} onClick={()=>{setReplying(false);setReply('');}}>Abbrechen</button>
        </div>
      </form>}
      {!closed&&loaded&&articles.length===0&&<p className="classic-wpf-help">
        Ohne Kundenartikel kann Plenaro keine sichere Empfängeradresse bestimmen.
        Bitte direkt in Znuny antworten.</p>}
    </div>
  </details>;
}
