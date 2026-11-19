import {afterEach,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {ZnunyTicketTimerPanel,parseTimerDuration,roundedBookingMinutes,
  quickAdjustUnbookedSeconds}
  from './ZnunyTicketTimerPanel';

afterEach(()=>{cleanup();vi.restoreAllMocks();vi.unstubAllGlobals();});

it('rounds only at booking: 1s->15m, 900s->15m, 901s->30m, 4200s->75m',()=>{
  for(const [seconds,expected] of [[1,15],[515,15],[900,15],[901,30],
    [1800,30],[4200,75],[4500,75],[4560,90]]){
    expect(roundedBookingMinutes(seconds)).toBe(expected);
  }
  expect(parseTimerDuration('01:23:45')).toBe(5025);
  expect(parseTimerDuration('01:75:00')).toBeNull();
  expect(parseTimerDuration('nonsense')).toBeNull();
});

it('shows exact per-agent daily reservation including pending bookings',async()=>{
  vi.stubGlobal('fetch',vi.fn(async (input:string)=>{
    const url=String(input);
    if(url==='/api/v1/znuny/tickets/time/daily-limit')
      return {ok:true,status:200,json:async()=>({
        day:'2026-11-19',limitMinutes:1440,
        reservedMinutes:1425,remainingMinutes:15
      })};
    if(url==='/api/v1/znuny/tickets/77/time')
      return {ok:true,status:200,json:async()=>({
        bookedSeconds:1800,runningSeconds:0,runningSince:null,
        adjustedMinutes:0,sessionCount:0,
        transferredSeconds:0,unbookedSeconds:1800
      })};
    if(url.endsWith('/time/bookings'))
      return {ok:true,status:200,json:async()=>([])};
    if(url.endsWith('/booking-fields'))
      return {ok:true,status:200,json:async()=>({
        costCenter:'',order:'',costCenterOptions:[],orderOptions:[]
      })};
    throw Error('Unexpected '+url);
  }));
  render(<ZnunyTicketTimerPanel ticketId="77"/>);
  expect(await screen.findByText(/Heute · 2026-11-19 · Znuny-Agent/)).toBeTruthy();
  expect(screen.getByText(/00:15 h noch frei/)).toBeTruthy();
  expect(screen.getByRole('progressbar',{
    name:'Heute in Znuny gebuchte oder reservierte Zeit'
  }).getAttribute('aria-valuenow')).toBe('1425');
  expect(screen.getByText(/Diese Buchung übersteigt das verbleibende Tageskontingent/))
    .toBeTruthy();
});

it('preserves seconds, clamps at zero/240 hours, and never adjusts transferred bookings',()=>{
  expect(quickAdjustUnbookedSeconds(901,15)).toBe(1801);
  expect(quickAdjustUnbookedSeconds(901,-15)).toBe(1);
  expect(quickAdjustUnbookedSeconds(600,-30)).toBe(0);
  expect(quickAdjustUnbookedSeconds(863900,60)).toBe(864000);
});

it('restores six WPF preset buttons and edits only unbooked time via PUT',async()=>{
  const targets:number[]=[];
  const time={
    bookedSeconds:7200,runningSeconds:0,runningSince:null,
    adjustedMinutes:0,sessionCount:1,
    transferredSeconds:5400,unbookedSeconds:1800
  };
  vi.stubGlobal('fetch',vi.fn(async (input:string,init?:RequestInit)=>{
    const url=String(input),method=init?.method??'GET';
    if(url==='/api/v1/auth/csrf')
      return {ok:true,status:200,json:async()=>({token:'csrf'})};
    if(url==='/api/v1/znuny/tickets/77/time' && method==='PUT'){
      const payload=JSON.parse(String(init?.body)) as {targetSeconds:number};
      targets.push(payload.targetSeconds);
      // Match the backend: the transferred baseline remains untouched.
      time.bookedSeconds=time.transferredSeconds+payload.targetSeconds;
      time.unbookedSeconds=payload.targetSeconds;
      return {ok:true,status:200,json:async()=>({...time})};
    }
    if(url==='/api/v1/znuny/tickets/77/time')
      return {ok:true,status:200,json:async()=>({...time})};
    if(url.endsWith('/time/bookings'))
      return {ok:true,status:200,json:async()=>([])};
    if(url.endsWith('/booking-fields'))
      return {ok:true,status:200,json:async()=>({
        costCenter:'100',order:'200',costCenterOptions:[],orderOptions:[]
      })};
    throw Error('Unexpected '+method+' '+url);
  }));
  render(<ZnunyTicketTimerPanel ticketId="77"/>);
  const names=[
    'Zeit erhöhen um 15 Minuten','Zeit erhöhen um 30 Minuten',
    'Zeit erhöhen um 60 Minuten (1 Stunde)',
    'Zeit verringern um 15 Minuten','Zeit verringern um 30 Minuten',
    'Zeit verringern um 60 Minuten (1 Stunde)'
  ];
  for(const name of names)expect(await screen.findByRole('button',{name})).toBeTruthy();
  for(const [i,name] of [
    names[0],names[1],names[3],names[4],names[5],names[2]
  ].entries()){
    const button=screen.getByRole('button',{name});
    await waitFor(()=>expect((button as HTMLButtonElement).disabled).toBe(false));
    fireEvent.click(button);
    await waitFor(()=>expect(targets.length).toBe(i+1));
  }
  expect(targets).toEqual([2700,4500,3600,1800,0,3600]);
  expect(time.transferredSeconds).toBe(5400);
  expect(time.bookedSeconds).toBe(9000);
  expect(screen.getByRole('button',{name:names[5]}).textContent).toBe('-1h');
  expect(await screen.findByText(/Noch nicht in Znuny gebucht: 60 Min/)).toBeTruthy();
});

it('disables corrections when a remote booking is unresolved',async()=>{
  vi.stubGlobal('fetch',vi.fn(async (input:string)=>{
    const url=String(input);
    if(url.endsWith('/time'))return {ok:true,status:200,json:async()=>({
      bookedSeconds:900,runningSeconds:0,runningSince:null,
      adjustedMinutes:0,sessionCount:0,transferredSeconds:0,unbookedSeconds:900
    })};
    if(url.endsWith('/time/bookings'))return {ok:true,status:200,json:async()=>[{
      id:'unresolved',status:'pending',sourceSeconds:900,bookedMinutes:15,
      costCenter:'',order:'',shortDescription:'',note:'',
      createdAt:'2026-11-17T06:00:00Z'
    }]};
    if(url.endsWith('/booking-fields'))return {ok:true,status:200,json:async()=>({
      costCenter:'',order:'',costCenterOptions:[],orderOptions:[]
    })};
    throw Error('Unexpected '+url);
  }));
  render(<ZnunyTicketTimerPanel ticketId="77"/>);
  await screen.findByText(/Vorherige Buchung noch nicht bestätigt/);
  for(const button of screen.getAllByRole('button')
    .filter(b=>/^(?:Zeit erhöhen|Zeit verringern) um /.test(b.getAttribute('aria-label')??''))){
    expect((button as HTMLButtonElement).disabled).toBe(true);
  }
});

it('clicks the unbooked clock to edit seconds without the old correction form',async()=>{
  const calls:Array<{url:string,method:string,body:unknown}>=[];
  const time={
    bookedSeconds:901,runningSeconds:0,runningSince:null,
    adjustedMinutes:0,sessionCount:1,
    transferredSeconds:0,unbookedSeconds:901,
  };
  vi.stubGlobal('fetch',vi.fn(async (input:string,init?:RequestInit)=>{
    const url=String(input),method=init?.method??'GET';
    calls.push({url,method,body:init?.body?JSON.parse(String(init.body)) as unknown:null});
    if(url==='/api/v1/znuny/tickets/77/time'&&method==='PUT'){
      time.bookedSeconds=4501;time.unbookedSeconds=4501;
      return {ok:true,status:200,json:async()=>({...time})};
    }
    if(url==='/api/v1/znuny/tickets/77/time')
      return {ok:true,status:200,json:async()=>({...time})};
    if(url.endsWith('/time/bookings'))
      return {ok:true,status:200,json:async()=>([])};
    if(url.endsWith('/booking-fields'))
      return {ok:true,status:200,json:async()=>({
        costCenter:'100',order:'200',
        costCenterOptions:[{key:'100',displayText:'IT'}],
        orderOptions:[{key:'200',displayText:'Service'}]
      })};
    if(url==='/api/v1/auth/csrf')
      return {ok:true,status:200,json:async()=>({token:'test'})};
    throw Error('Unexpected '+method+' '+url);
  }));
  render(<ZnunyTicketTimerPanel ticketId="77"/>);
  expect(await screen.findByRole('button',{name:'Timerzeit bearbeiten'})).toBeTruthy();
  expect(screen.queryByText(/Korrektur \(Minuten/)).toBeNull();
  fireEvent.click(screen.getByRole('button',{name:'Timerzeit bearbeiten'}));
  fireEvent.change(screen.getByLabelText('Timerzeit bearbeiten'),
    {target:{value:'01:15:01'}});
  fireEvent.click(screen.getByRole('button',{name:'Timerzeit speichern'}));
  await waitFor(()=>expect(calls.some(c=>c.method==='PUT' &&
    c.url==='/api/v1/znuny/tickets/77/time' &&
    (c.body as {targetSeconds:number}).targetSeconds===4501)).toBe(true));
  expect(await screen.findByText(/an Znuny: 90 Min/)).toBeTruthy();
  expect(screen.getByRole('combobox',{name:'Kostenstelle'})).toBeTruthy();
  expect(screen.getByRole('combobox',{name:'Auftrag'})).toBeTruthy();
});

it('stops a running timer before submitting one confirmed Znuny time booking',async()=>{
  const calls:string[]=[];
  const time={
    bookedSeconds:0,runningSeconds:0,runningSince:new Date(Date.now()-61_000).toISOString(),
    adjustedMinutes:0,sessionCount:0,transferredSeconds:0,unbookedSeconds:0
  };
  vi.spyOn(window,'confirm').mockReturnValue(true);
  vi.stubGlobal('fetch',vi.fn(async(input:string,init?:RequestInit)=>{
    const url=String(input),method=init?.method??'GET';
    if(url==='/api/v1/auth/csrf')
      return {ok:true,status:200,json:async()=>({token:'csrf'})};
    if(url.endsWith('/booking-fields'))
      return {ok:true,status:200,json:async()=>({
        costCenter:'100',order:'200',costCenterOptions:[{key:'100',displayText:'IT'}],
        orderOptions:[{key:'200',displayText:'Test'}]
      })};
    if(url.endsWith('/time/bookings'))
      return {ok:true,status:200,json:async()=>([])};
    if(url.endsWith('/time')&&method==='GET')
      return {ok:true,status:200,json:async()=>({...time})};
    if(url.endsWith('/time/stop')&&method==='POST'){
      calls.push('stop');
      time.bookedSeconds=61;time.unbookedSeconds=61;time.runningSince='';
      return {ok:true,status:200,json:async()=>({...time,runningSince:null})};
    }
    if(url.endsWith('/time/book')&&method==='POST'){
      calls.push('book');
      return {ok:true,status:200,json:async()=>({
        confirmed:true,id:'fake',sourceSeconds:61,bookedMinutes:15
      })};
    }
    throw Error('Unexpected '+method+' '+url);
  }));
  render(<ZnunyTicketTimerPanel ticketId="77"/>);
  const button=await screen.findByRole('button',{name:'Im Ticketsystem buchen'});
  await waitFor(()=>expect((button as HTMLButtonElement).disabled).toBe(false));
  fireEvent.click(button);
  await waitFor(()=>expect(calls).toEqual(['stop','book']));
  expect(await screen.findByText(/15 Minuten wurden von Znuny bestätigt/)).toBeTruthy();
});
