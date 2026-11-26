import { useEffect, useState } from 'react';
import { CalendarDays } from 'lucide-react';
import { saveDayMarker, type DayMarker, type DayType } from './dayMarkersApi';
import {
  GermanDay, preflightHomeofficeMail, readBrMailStatus, reconcileHomeOfficeCalendar,
  sendBrRequest, type BrMailStatus,
} from './brRequestsApi';

type MarkerChoice = { dayType:DayType; isBr:boolean; isHo:boolean };
type Pending = { choice:MarkerChoice; needsMail:boolean };

export function WeekDayMarkerControls({day,marker,onSaved}:{
  day:string;marker?:DayMarker;onSaved:(next:DayMarker)=>void;
}) {
  const [busy,setBusy]=useState(false);
  const [pending,setPending]=useState<Pending|null>(null);
  const [mailStatus,setMailStatus]=useState<BrMailStatus|null>(null);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');

  useEffect(()=>{
    let active=true;
    setMailStatus(null);setError('');setNotice('');setPending(null);
    void readBrMailStatus(day).then(status=>{
      if(active)setMailStatus(status);
    }).catch(()=>{if(active)setMailStatus(null);});
    return()=>{active=false;};
  },[day]);

  const current:MarkerChoice={
    dayType:marker?.dayType??'Normal',
    isBr:marker?.isBr??false,
    isHo:marker?.isHo??false,
  };

  function select(next:MarkerChoice){
    if(busy||pending||
       (next.dayType===current.dayType&&
        next.isBr===current.isBr&&next.isHo===current.isHo))return;
    setError('');setNotice('');
    const newlyHo=next.isHo&&!current.isHo;
    const newlyBr=next.isBr&&!current.isBr;
    // Keep the existing DayMarkersPanel/WPF side effects: a fresh HO
    // request requires a calendar event and mail; BR alone sends only mail.
    // Unknown server state must never silently suppress confirmation.
    const needsMail=newlyHo||(newlyBr&&
      mailStatus?.status!=='accepted'&&
      mailStatus?.status!=='unknown'&&
      mailStatus?.status!=='processing');
    if(needsMail){setPending({choice:next,needsMail});return;}
    void persist(next,false);
  }

  async function persist(next:MarkerChoice,needsMail:boolean){
    if(busy)return;
    setPending(null);setBusy(true);setError('');setNotice('');
    let stored=false;
    try{
      if(needsMail)await preflightHomeofficeMail(next.isHo);
      await saveDayMarker(day,next.dayType,next.isBr,next.isHo);
      stored=true;
      onSaved({day,...next});
      if(needsMail){
        try{
          const result=await sendBrRequest(day);
          setMailStatus({status:'accepted'});
          setNotice(result.message);
        }catch(e){
          setError('Tagesmarker gespeichert, aber Outlook-Termin/E-Mail nicht vollständig bestätigt: '+
            (e instanceof Error?e.message:'Unbekannter Fehler.'));
        }
      }else setNotice('Tagesmarker gespeichert.');
    }catch(e){
      setError((stored?'Tagesmarker gespeichert. ':
        needsMail?'Homeoffice/BR wurde nicht beantragt. ':'')+
        (e instanceof Error?e.message:'Speichern fehlgeschlagen.'));
    }finally{setBusy(false);}
  }

  async function reconcileHo(){
    if(busy||!marker?.isHo)return;
    setBusy(true);setError('');setNotice('');
    try{
      const result=await reconcileHomeOfficeCalendar(day);
      setNotice(result.message);
    }catch(e){
      setError(e instanceof Error?e.message:'Outlook-Homeoffice-Termin konnte nicht überprüft werden.');
    }finally{setBusy(false);}
  }

  return <div className="classic-week-marker-editor" aria-label="Tagesmarker direkt bearbeiten">
    <div className="classic-week-marker-options" role="group" aria-label="Tagesart">
      {(['Normal','UL','AM'] as DayType[]).map(type=>
        <button key={type} type="button" className={'plenaro-marker-pill'+
          (current.dayType===type?' active':'')}
          aria-pressed={current.dayType===type} disabled={busy||!!pending}
          onClick={()=>select({...current,dayType:type})}>{type}</button>)}
    </div>
    <div className="classic-week-marker-options" role="group" aria-label="Zusatzmarker">
      <button type="button" className={'plenaro-marker-pill'+(current.isHo?' active':'')}
        aria-pressed={current.isHo} disabled={busy||!!pending}
        onClick={()=>select({...current,isHo:!current.isHo})}>HO</button>
      <button type="button" className={'plenaro-marker-pill'+(current.isBr?' active':'')}
        aria-pressed={current.isBr} disabled={busy||!!pending}
        onClick={()=>select({...current,isBr:!current.isBr})}>BR</button>
    </div>
    {busy&&<p role="status" className="um-hint">Tagesmarker wird gespeichert …</p>}
    {error&&<p role="alert" className="um-feedback failure">{error}</p>}
    {notice&&<p role="status" className="um-feedback success">{notice}</p>}
    {marker?.isHo&&<button type="button" className="classic-text-action"
      onClick={()=>void reconcileHo()} disabled={busy}>
      <CalendarDays size={14}/>Outlook-Homeoffice-Termin prüfen / nachtragen
    </button>}
    {pending&&<div className="classic-week-marker-confirm" role="dialog"
      aria-label="Tagesmarker beantragen" aria-modal="false">
      <strong>{pending.choice.isHo?'Homeoffice':'BR'} beantragen</strong>
      <p>Willst du wirklich am {GermanDay(day)} den Tagesmarker beantragen?
        {pending.choice.isHo
          ?' Plenaro prüft/erstellt zuerst einen ganztägigen Outlook-Termin („An anderem Ort tätig“) und versendet die E-Mail.'
          :' Plenaro versendet die BR-E-Mail.'}</p>
      <div className="classic-week-marker-confirm-actions">
        <button className="button button-secondary" type="button"
          onClick={()=>setPending(null)}>Nein</button>
        <button className="button button-primary" type="button"
          onClick={()=>void persist(pending.choice,pending.needsMail)}>
          Ja, beantragen</button>
      </div>
    </div>}
  </div>;
}
