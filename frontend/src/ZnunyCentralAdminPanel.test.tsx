import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ZnunyCentralAdminPanel } from './ZnunyCentralAdminPanel';
import type { SyncStatus } from './znunySyncApi';

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it('saves the new 365-day lookback before restarting bootstrap without clearing cache', async () => {
  const status: SyncStatus = {
    enabled:false,serviceUsername:'ci-sync',passwordConfigured:false,
    sourceApiUrl:'https://znuny.example.invalid/api',initialLookbackDays:7,
    deltaIntervalMinutes:15,openIntervalMinutes:360,
    maxRequestsPerCycle:200,candidateDefaultAgentId:1,
    bootstrapCursor:'2026-10-20T00:00:00Z',
    bootstrapEnd:'2026-10-27T00:00:00Z',
    bootstrapFinishedAt:'2026-10-27T00:00:00Z',
    deltaWatermark:'2026-10-27T00:00:00Z',
    lastDeltaAt:'2026-10-27T01:00:00Z',
    lastOpenAt:null,openCursor:null,lastAttemptAt:null,lastError:null,cachedTickets:31,
  };
  const calls: Array<{url:string;method:string;body:unknown}> = [];
  vi.spyOn(window,'confirm').mockReturnValue(true);
  vi.stubGlobal('fetch', vi.fn(async (input: string, init?: RequestInit) => {
    const url=String(input);
    const method=init?.method??'GET';
    calls.push({url,method,body:init?.body ? JSON.parse(String(init.body)) as unknown : null});
    if(url==='/api/v1/auth/csrf')
      return {ok:true,status:200,json:async()=>({token:'test-csrf'})};
    if(url==='/api/v1/admin/znuny/sync'&&method==='GET')
      return {ok:true,status:200,json:async()=>({...status})};
    if(url==='/api/v1/admin/znuny/sync'&&method==='PUT'){
      status.initialLookbackDays=365;
      return {ok:true,status:204};
    }
    if(url==='/api/v1/admin/znuny/sync/bootstrap/restart'&&method==='POST')
      return {ok:true,status:200,json:async()=>({started:false,status:'disabled',requests:0,tickets:0})};
    throw new Error('Unexpected URL '+method+' '+url);
  }));
  render(<ZnunyCentralAdminPanel/>);
  fireEvent.change(await screen.findByLabelText('Erstimport rückwirkend (Tage)'),
    {target:{value:'365'}});
  fireEvent.click(screen.getByRole('button',{name:'Erstimport neu starten'}));
  await waitFor(()=>expect(calls.some(x=>x.url.endsWith('/sync/bootstrap/restart'))).toBe(true));
  const saves=calls.filter(x=>x.method==='PUT'&&x.url==='/api/v1/admin/znuny/sync');
  expect((saves[0].body as {initialLookbackDays:number}).initialLookbackDays).toBe(365);
  const restart=calls.find(x=>x.url.endsWith('/sync/bootstrap/restart'))!;
  expect(restart.body).toEqual({confirmation:'ZNUNY ERSTIMPORT NEU STARTEN'});
  expect(calls.findIndex(x=>x.method==='PUT'&&x.url==='/api/v1/admin/znuny/sync'))
    .toBeLessThan(calls.findIndex(x=>x.url.endsWith('/sync/bootstrap/restart')));
  expect(calls.some(x=>x.url.endsWith('/sync/reset'))).toBe(false);
  expect(await screen.findByText(/Erstimport für 365 Tage vorgemerkt/)).toBeTruthy();
});
