import {useEffect,useState,type FormEvent} from 'react';
import {Mail,Save} from 'lucide-react';
import {readBrRecipients,saveBrRecipients,type BrRecipients} from './brRequestsApi';

export function BrRecipientsSettingsPanel(){
  const [form,setForm]=useState<BrRecipients>({recipient1:'',recipient2:''});
  const [loading,setLoading]=useState(true);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');

  useEffect(()=>{
    let mounted=true;
    void readBrRecipients().then(p=>{
      if(mounted){setForm(p);setLoading(false);}
    }).catch(e=>{
      if(mounted){setError(e instanceof Error?e.message:'Empfänger konnten nicht geladen werden.');
        setLoading(false);}
    });
    return()=>{mounted=false;};
  },[]);

  async function submit(e:FormEvent){
    e.preventDefault();if(busy)return;
    setBusy(true);setError('');setNotice('');
    try{
      await saveBrRecipients(form);
      setNotice('Deine E-Mail-Empfänger wurden gespeichert.');
    }catch(reason){
      setError(reason instanceof Error?reason.message:'Speichern fehlgeschlagen.');
    }finally{setBusy(false);}
  }
  return <section className="content-card um-panel" aria-labelledby="br-mail-settings">
    <div className="um-section-title"><Mail size={20}/>
      <h2 id="br-mail-settings">BR-Beantragung per E-Mail</h2>
    </div>
    <p>Lege selbst fest, wer bei einem bestätigten BR-Antrag eine E-Mail erhält.
      Wie im früheren Plenaro gibt es zwei persönliche Empfänger, der zweite ist optional.</p>
    {loading?<p role="status">Lade BR-Empfänger …</p>:
    <form className="um-form" onSubmit={e=>void submit(e)}>
      <label htmlFor="br-recipient-1">E-Mail-Empfänger 1</label>
      <input id="br-recipient-1" type="email" maxLength={254}
        placeholder="vorgesetzter@firma.de" autoComplete="off"
        value={form.recipient1}
        onChange={e=>setForm({...form,recipient1:e.target.value})}/>
      <label htmlFor="br-recipient-2">E-Mail-Empfänger 2 (optional)</label>
      <input id="br-recipient-2" type="email" maxLength={254}
        placeholder="team@firma.de" autoComplete="off"
        value={form.recipient2}
        onChange={e=>setForm({...form,recipient2:e.target.value})}/>
      <p className="um-hint">Der Versand erfolgt über dein persönlich verbundenes
        Microsoft-365-Postfach. Dafür benötigt Plenaro die delegierte Berechtigung
        Mail.Send. Nach einem Update muss Microsoft 365 möglicherweise erneut verbunden werden.</p>
      <a className="button button-secondary" href="/api/v1/graph/connect/start">
        Outlook verbinden / Mail.Send freigeben</a>
      <p className="um-hint">Ein BR-Marker allein sendet keine E-Mail. Beim erstmaligen
        Markieren fragt Plenaro nach deiner Bestätigung. Bereits angenommene
        oder unklare Versandversuche werden nicht automatisch wiederholt.</p>
      <button className="button button-primary" type="submit" disabled={busy}>
        <Save size={16}/>{busy?'Speichere …':'Empfänger speichern'}</button>
    </form>}
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {notice&&<p role="status" className="um-feedback success">{notice}</p>}
  </section>;
}
