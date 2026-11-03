export type SyncStatus = {
  enabled: boolean; serviceUsername: string; passwordConfigured: boolean;
  sourceApiUrl: string | null; initialLookbackDays: number;
  deltaIntervalMinutes: number; openIntervalMinutes: number;
  maxRequestsPerCycle: number; candidateDefaultAgentId: number;
  bootstrapCursor: string | null;
  bootstrapEnd: string | null; bootstrapFinishedAt: string | null;
  deltaWatermark: string | null; lastDeltaAt: string | null;
  lastOpenAt: string | null; openCursor: string | null;
  lastAttemptAt: string | null; lastError: string | null; cachedTickets: number;
};
export type SyncConfig = Pick<SyncStatus,'enabled'|'serviceUsername'|'initialLookbackDays'|
  'deltaIntervalMinutes'|'openIntervalMinutes'|'maxRequestsPerCycle'|'candidateDefaultAgentId'>;
export type SyncRunResult = {
  started: boolean; status: string; requests: number; tickets: number;
};
export type CachedTicket = {
  ticketId: string; ticketNumber: string; title: string; queue: string;
  state: string; stateType: string; ownerId: number|null; responsibleId: number|null;
  createdAt: string|null; changedAt: string|null;
};
export type TicketAccess = {
  approved:boolean;agentId:number|null;candidateUserId:number|null;syncEnabled:boolean;
  showAllCandidatePoolTickets:boolean;hasCandidateKeywords:boolean;
};
export type TicketSegment = {
  id: string; startsAt: string; endsAt: string; note: string;
  participants: string[];
};
export type TicketWorkspace = { description:string; ticketUrl:string };
export type TicketTimeSummary = {
  bookedSeconds:number;runningSeconds:number;runningSince:string|null;
  adjustedMinutes:number;sessionCount:number;
  transferredSeconds:number;unbookedSeconds:number;
};
export type ZnunyFieldOption = { key:string;displayText:string };
export type ZnunyBookingFields = {
  costCenter:string;order:string;
  costCenterOptions:ZnunyFieldOption[];orderOptions:ZnunyFieldOption[];
};
export type ZnunyBooking = {
  id:string;sourceSeconds:number;bookedMinutes:number;costCenter:string;
  order:string;shortDescription:string;note:string;
  status:'pending'|'confirmed'|'rejected';
  rejectionCode:string|null;articleId:string|null;createdAt:string;
};
export type ZnunyBooked = {
  confirmed:boolean;id:string;sourceSeconds:number;bookedMinutes:number;
};
export type TicketAgendaSegment = TicketSegment & CachedTicket;

async function request<T>(url: string): Promise<T> {
  const result=await fetch(url,{credentials:'same-origin',cache:'no-store'});
  if(!result.ok){
    const details=await result.json().catch(()=>({})) as {error?:string;detail?:string};
    throw new Error(details.error||details.detail||
      'Znuny-Daten konnten nicht geladen werden (HTTP '+result.status+').');
  }
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
    const details=await response.json().catch(()=>({})) as {error?:string;detail?:string};
    throw new Error(details.error||details.detail||
      'Vorgang fehlgeschlagen (HTTP '+response.status+').');
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
/** Restart the historical scan while preserving cache, assignments and saved ticket plans. */
export const restartInitialImport=async()=>{
  return await (await mutate('/api/v1/admin/znuny/sync/bootstrap/restart','POST',
    {confirmation:'ZNUNY ERSTIMPORT NEU STARTEN'})).json() as SyncRunResult;
};
export const resetCentralCache=async()=>{
  await mutate('/api/v1/admin/znuny/sync/reset','POST',{confirmation:'ZNUNY CACHE ZURUECKSETZEN'});
};
export const getTicketAccess=()=>request<TicketAccess>('/api/v1/znuny/tickets/status');
export type TicketListFilters = {
  query:string;queue:string;state:string;
  sort:'changed'|'created'|'title'|'number';direction:'asc'|'desc';
};
export const getCachedTickets=(view:'mine'|'new'|'completed'='mine',search='',page=0,
  filters:Partial<TicketListFilters>={})=>
  request<CachedTicket[]>('/api/v1/znuny/tickets?'+new URLSearchParams({
    view,search:search.trim(),page:String(page),
    queue:filters.queue??'',state:filters.state??'',
    sort:filters.sort??'changed',direction:filters.direction??'desc'
  }));
export const setShowAllCandidates=async(showAll:boolean)=>{
  await mutate('/api/v1/znuny/tickets/candidates/show-all','PUT',{showAll});
};

export const getTicketAgenda=(day:string)=>
  request<TicketAgendaSegment[]>('/api/v1/znuny/tickets/agenda?date='+encodeURIComponent(day));
export type BusyInterval = {id:string;startsAt:string;endsAt:string};
export type TicketDayAvailability = {intervals:BusyInterval[];hasMore:boolean};
export const readTicketDayAvailability=(ticketId:string,day:string)=>
  request<TicketDayAvailability>('/api/v1/znuny/tickets/'+encodeURIComponent(ticketId)+
    '/availability?date='+encodeURIComponent(day));
export const getTicketSegments=(ticketId:string)=>
  request<TicketSegment[]>('/api/v1/znuny/tickets/'+encodeURIComponent(ticketId)+'/segments');
export const planTicketSegment=async(ticketId:string,input:{
  startsAt:string;endsAt:string;note:string;participants:string[];
})=>{
  await mutate('/api/v1/znuny/tickets/'+encodeURIComponent(ticketId)+'/segments','POST',input);
};
export const deleteTicketSegment=async(ticketId:string,segmentId:string)=>{
  await mutate('/api/v1/znuny/tickets/'+encodeURIComponent(ticketId)+
    '/segments/'+encodeURIComponent(segmentId),'DELETE');
};

export const updateTicketSegment=async(ticketId:string,segmentId:string,input:{
  startsAt:string;endsAt:string;note:string;participants:string[];
})=>{
  await mutate('/api/v1/znuny/tickets/'+encodeURIComponent(ticketId)+
    '/segments/'+encodeURIComponent(segmentId),'PUT',input);
};
const workspaceBase=(ticketId:string)=>
  '/api/v1/znuny/tickets/'+encodeURIComponent(ticketId);
export const readTicketWorkspace=(ticketId:string)=>
  request<TicketWorkspace>(workspaceBase(ticketId)+'/workspace');
export const saveTicketWorkspace=async(ticketId:string,values:TicketWorkspace)=>{
  await mutate(workspaceBase(ticketId)+'/workspace','PUT',values);
};
export const readZnunyBookingFields=(ticketId:string)=>
  request<ZnunyBookingFields>(workspaceBase(ticketId)+'/booking-fields');
export const readZnunyBookings=(ticketId:string)=>
  request<ZnunyBooking[]>(workspaceBase(ticketId)+'/time/bookings');
export const setTicketUnbookedSeconds=async(ticketId:string,targetSeconds:number)=>
  await (await mutate(workspaceBase(ticketId)+'/time','PUT',
    {targetSeconds})).json() as TicketTimeSummary;
export const bookZnunyTime=async(ticketId:string,input:{
  costCenter:string;order:string;shortDescription:string;note:string;
})=>await (await mutate(workspaceBase(ticketId)+'/time/book','POST',
  input)).json() as ZnunyBooked;
export const reconcileZnunyBooking=async(ticketId:string,bookingId:string)=>
  await (await mutate(workspaceBase(ticketId)+'/time/bookings/'+
    encodeURIComponent(bookingId)+'/reconcile','POST')).json() as {
      confirmed:boolean;pending?:boolean;message?:string;
    };
export const readTicketTime=(ticketId:string)=>
  request<TicketTimeSummary>(workspaceBase(ticketId)+'/time');
export const startTicketTime=async(ticketId:string)=>
  await (await mutate(workspaceBase(ticketId)+'/time/start','POST')).json() as TicketTimeSummary;
export const stopTicketTime=async(ticketId:string)=>
  await (await mutate(workspaceBase(ticketId)+'/time/stop','POST')).json() as TicketTimeSummary;
export const adjustTicketTime=async(ticketId:string,deltaMinutes:number,note='')=>
  await (await mutate(workspaceBase(ticketId)+'/time/adjust','POST',
    {deltaMinutes,note})).json() as TicketTimeSummary;

export type ZnunyAttachment = {
  id:string;filename:string;contentType:string;contentId:string;
  size:number;disposition:string;
};
export type ZnunyArticle = {
  id:string;subject:string;body:string;from:string;to:string;replyTo:string;
  senderType:string;channel:string;created:string;contentType?:string;
  attachments?:ZnunyAttachment[];
};
export type ZnunyConversation = {
  ticketId:string;ticketNumber:string;title:string;articles:ZnunyArticle[];
};
export type ZnunyAgent = { id:number;name:string };
export type ZnunyUpdateConfirmation = { confirmed:boolean;cacheUpdated?:boolean };
export const readZnunyArticles=(ticketId:string)=>
  request<ZnunyConversation>(workspaceBase(ticketId)+'/articles');
export const checkZnunyWriteReadiness=(ticketId:string)=>
  request<{personalLogin:boolean;ticketGet:boolean;writeMethod:string;
    writeRoute:string;writeVerified:boolean;message:string}>(
    workspaceBase(ticketId)+'/write-check');
/** Download always comes from Plenaro's authenticated, ticket-scoped proxy. */
export const znunyAttachmentDownloadUrl=(ticketId:string,articleId:string,fileId:string)=>
  workspaceBase(ticketId)+'/articles/'+encodeURIComponent(articleId)+
  '/attachments/'+encodeURIComponent(fileId);
export const readZnunyInlineImage=(ticketId:string,articleId:string,fileId:string)=>
  request<{dataUri:string}>(znunyAttachmentDownloadUrl(ticketId,articleId,fileId)+'/inline');
export const readZnunyAgents=()=>
  request<ZnunyAgent[]>('/api/v1/znuny/agents');
export const updateZnunyAssignment=async(ticketId:string,ownerId:number|null,
  responsibleId:number|null)=>{
  const result=await mutate(workspaceBase(ticketId)+'/assignment','PUT',{
    ownerId,responsibleId
  });
  return await result.json() as ZnunyUpdateConfirmation;
};
export const assignZnunyToMe=async(ticketId:string)=>{
  const result=await mutate(workspaceBase(ticketId)+'/assign-to-me','POST');
  return await result.json() as ZnunyUpdateConfirmation;
};
export const sendZnunyReply=async(ticketId:string,body:string,close=false)=>{
  const result=await mutate(workspaceBase(ticketId)+'/reply','POST',{body,close});
  return await result.json() as ZnunyUpdateConfirmation;
};

export const changeZnunyTicketState=async(ticketId:string,close:boolean)=>{
  const response=await mutate(workspaceBase(ticketId)+'/state','PUT',{close});
  return await response.json() as ZnunyUpdateConfirmation;
};
