import {afterEach,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {ZnunyTicketTimerPanel,parseTimerDuration,roundedBookingMinutes}
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
