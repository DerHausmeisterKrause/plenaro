import {afterEach,describe,expect,it,vi} from 'vitest';
import {getTicketAccess} from './znunySyncApi';

afterEach(()=>vi.unstubAllGlobals());

describe('Znuny dashboard HTTP 429 diagnostics',()=>{
  it('shows Plenaro quota and Retry-After instead of a generic Znuny failure',async()=>{
    vi.stubGlobal('fetch',vi.fn().mockResolvedValue(
      new Response(JSON.stringify({
        code:'plenaro-rate-limited',
        error:'Zu viele Plenaro-Anfragen für dieses Konto.'
      }),{status:429,headers:{
        'Content-Type':'application/json',
        'X-Plenaro-RateLimit':'application',
        'Retry-After':'35'
      }})
    ));
    await expect(getTicketAccess()).rejects.toThrow(
      'Zu viele Plenaro-Anfragen für dieses Konto. Erneut versuchen in etwa 35 Sekunden.');
  });

  it('does not mislabel an outer reverse-proxy 429 as a Znuny API error',async()=>{
    vi.stubGlobal('fetch',vi.fn().mockResolvedValue(
      new Response('<html>too many requests</html>',{status:429})
    ));
    await expect(getTicketAccess()).rejects.toThrow(
      'Der vorgeschaltete Webserver hat die Anfrage begrenzt.');
  });

  it('retains normal successful dashboard reads',async()=>{
    const status={approved:true,agentId:42,syncEnabled:true,
      candidateUserId:1,showAllCandidatePoolTickets:false,
      hasCandidateKeywords:true};
    const fetcher=vi.fn().mockResolvedValue(new Response(JSON.stringify(status),
      {status:200,headers:{'Content-Type':'application/json'}}));
    vi.stubGlobal('fetch',fetcher);
    await expect(getTicketAccess()).resolves.toEqual(status);
    expect(fetcher).toHaveBeenCalledWith('/api/v1/znuny/tickets/status',
      {credentials:'same-origin',cache:'no-store'});
  });
});
