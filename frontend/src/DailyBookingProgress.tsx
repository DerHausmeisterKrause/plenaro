import type {ZnunyDailyBookingLimit} from './znunySyncApi';

/** The quota includes pending reservations. Backend writes enforce the actual
 * limit under a database lock; this is only an informational readout. */
export function DailyBookingProgress({quota,compact=false}:{
  quota:ZnunyDailyBookingLimit;compact?:boolean;
}){
  const limit=Math.max(1,quota.limitMinutes);
  const used=Math.max(0,quota.reservedMinutes);
  const remaining=Math.max(0,quota.remainingMinutes);
  const percent=Math.min(100,Math.max(0,used/limit*100));
  const duration=(minutes:number)=>{
    const safe=Math.max(0,Math.floor(minutes));
    return String(Math.floor(safe/60)).padStart(2,'0')+':'+
      String(safe%60).padStart(2,'0')+' h';
  };
  return <div className={'znuny-daily-progress'+(compact?' is-compact':'')}
    aria-label="Znuny-Zeitbuchungen heute">
    <div className="znuny-daily-progress-head">
      <span>Heute · {quota.day} · Znuny-Agent</span>
      <strong>{duration(used)} / {duration(limit)}</strong>
    </div>
    <div className="znuny-daily-progress-track" role="progressbar"
      aria-label="Heute in Znuny gebuchte oder reservierte Zeit"
      aria-valuemin={0} aria-valuemax={limit}
      aria-valuenow={Math.min(used,limit)}
      aria-valuetext={duration(used)+' von '+duration(limit)+' gebucht oder reserviert'}>
      <div className={'znuny-daily-progress-fill'+
          (percent>=100?' is-full':percent>=90?' is-near':'')}
        style={{width:percent+'%'}}/>
    </div>
    <div className="znuny-daily-progress-footer">
      <span>Gebucht / noch ungeklärt: {duration(used)}</span>
      <strong>{duration(remaining)} noch frei</strong>
    </div>
    {!compact&&<p className="um-hint">
      Pro Agent und Berliner Kalendertag maximal 24 Stunden.
      Gezählt werden alle Plenaro-Buchungen inklusive noch ungeklärter
      Übertragungen. Direkte Buchungen außerhalb Plenaros sind nicht erfasst.
    </p>}
  </div>;
}
