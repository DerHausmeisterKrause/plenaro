export type SyncStatus = {
  enabled: boolean; serviceUsername: string; passwordConfigured: boolean;
  sourceApiUrl: string | null; initialLookbackDays: number;
  deltaIntervalMinutes: number; openIntervalMinutes: number;
  maxRequestsPerCycle: number; bootstrapCursor: string | null;
  bootstrapEnd: string | null; bootstrapFinishedAt: string | null;
  deltaWatermark: string | null; lastDeltaAt: string | null;
  lastOpenAt: string | null; openCursor: string | null;
  lastAttemptAt: string | null; lastError: string | null; cachedTickets: number;
};
export type SyncConfig = Pick<SyncStatus,'enabled'|'serviceUsername'|'initialLookbackDays'|
  'deltaIntervalMinutes'|'openIntervalMinutes'|'maxRequestsPerCycle'>;
export type AgentBinding = {
  userId: string; email: string; approvedAgentId: number|null; requestedAgentId: number;
};
export type SyncRunResult = {
  started: boolean; status: string; requests: number; tickets: number;
};
export type CachedTicket = {
  ticketId: string; ticketNumber: string; title: string; queue: string;
  state: string; stateType: string; ownerId: number|null; responsibleId: number|null;
  createdAt: string|null; changedAt: string|null;
};
export type TicketAccess = { approved: boolean; agentId:number|null; syncEnabled:boolean };

async function request<T>(url: string): Promise<T> {
  const result=await fetch(url,{credentials:'same-origin',cache:'no-store'});
  if(!result.ok)throw new Error('Znuny-Daten konnten nicht geladen werden.');
  return await result.json() as T;
}
async function mutate(url:string,method:'PUT'|'DELETE'|'POST',body?:unknown):Promise<Response>{
  const csrf=await request<{token:string}>('/api/v1/auth/csrf');
  const response=await fetch(url,{
    method,credentials:'same-origin',
    headers:{'X-CSRF-TOKEN':csrf.token,...(body!==undefined?{'Content-Type':'application/json'}:{})},
    ...(body!==undefined?{body:JSON.stringify(body)}:{}),
  });
  if(!response.ok){
    const details=await response.json().catch(()=>({})) as {error?:string};
    throw new Error(details.error||'Vorgang fehlgeschlagen (HTTP '+response.status+').');
  }
  return response;
}
export const getSyncStatus=()=>request<SyncStatus>('/api/v1/admin/znuny/sync');
export const saveSyncConfig=async(settings:SyncConfig)=>{
  await mutate('/api/v1/admin/znuny/sync','PUT',settings);
};
export const saveSyncPassword=async(password:string)=>{
  await mutate('/api/v1/admin/znuny/sync/password','PUT',{password});
};
export const deleteSyncPassword=async()=>{
  await mutate('/api/v1/admin/znuny/sync/password','DELETE');
};
export const runCentralSync=async()=>{
  return await (await mutate('/api/v1/admin/znuny/sync/run','POST')).json() as SyncRunResult;
};
export const resetCentralCache=async()=>{
  await mutate('/api/v1/admin/znuny/sync/reset','POST',{confirmation:'ZNUNY CACHE ZURUECKSETZEN'});
};
export const getAgentBindings=()=>request<AgentBinding[]>('/api/v1/admin/znuny/bindings');
export const approveAgent=async(userId:string,agentId:number)=>{
  await mutate('/api/v1/admin/znuny/bindings/'+encodeURIComponent(userId),'PUT',{agentId});
};
export const revokeAgent=async(userId:string)=>{
  await mutate('/api/v1/admin/znuny/bindings/'+encodeURIComponent(userId),'DELETE');
};
export const getTicketAccess=()=>request<TicketAccess>('/api/v1/znuny/tickets/status');
export const getCachedTickets=(view:'mine'|'new'='mine',search='')=>
  request<CachedTicket[]>('/api/v1/znuny/tickets?view='+view+'&search='+encodeURIComponent(search));
