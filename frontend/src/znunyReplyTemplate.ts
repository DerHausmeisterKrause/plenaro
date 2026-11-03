import type {ZnunyArticle} from './znunySyncApi';

const variables=/\$(CustomerFirstName|CustomerLastName|TicketNumber|TicketTitle|Customer|Date)(?![A-Za-z0-9_])/g;
export const closeNotice='Ist erfolgt. Ticket wird geschlossen.';

export function customerName(from:string):string {
  const match=/^\s*(?:"([^"]+)"|([^<>]+?))\s*<[^<>]+>\s*$/.exec(from);
  return (match?.[1]??match?.[2]??'').trim();
}
export function customerArticle(articles:ZnunyArticle[]):ZnunyArticle|undefined {
  const customers=[...articles].reverse().filter(article=>
    article.senderType.toLowerCase().includes('customer') ||
    article.channel.toLowerCase().includes('email-external'));
  return customers.find(article=>article.channel.toLowerCase().includes('email')) ??
    customers[0];
}
export function renderReplyTemplate(template:string,original:ZnunyArticle|undefined,
  ticketNumber:string,ticketTitle:string,localDate=new Date()):
  {text:string;unresolved:boolean} {
  const customer=customerName(original?.from??'');
  let first='',last='';
  if(customer.includes(',')&&customer.split(',').length===2){
    const [family,given]=customer.split(',').map(s=>s.trim());
    first=given.split(/\s+/)[0]??'';last=family;
  }else{
    const parts=customer.split(/\s+/).filter(Boolean);
    if(parts.length>=2){first=parts[0];last=parts[parts.length-1];}
  }
  const values:Record<string,string>={
    '$Customer':customer,'$CustomerFirstName':first,'$CustomerLastName':last,
    '$TicketNumber':ticketNumber,'$TicketTitle':ticketTitle,
    '$Date':new Intl.DateTimeFormat('de-DE',{day:'2-digit',month:'2-digit',
      year:'numeric',timeZone:'Europe/Berlin'}).format(localDate)
  };
  // Old settings examples accidentally contained literal backslash-n
  // sequences. Preserve real line breaks and accept those older values.
  const normalized=template.slice(0,10000).replace(/\\r\\n|\\n/g,'\n');
  const text=normalized.replace(variables,match=>values[match]?.trim()||match);
  return {text,unresolved: new RegExp(variables.source,'g').test(text)};
}
export function ensureCloseNotice(text:string):string {
  const normalized=text.replace(/\r\n?/g,'\n').trim();
  if(normalized.includes(closeNotice))return normalized;
  if(!normalized)return closeNotice;
  const lines=normalized.split('\n');
  const first=lines.findIndex(line=>line.trim().length>0);
  if(first<0)return closeNotice;
  const before=lines.slice(0,first+1).map(s=>s.trimEnd());
  const after=lines.slice(first+1);
  while(after.length&&after[0].trim().length===0)after.shift();
  const trailing=after.map(x=>x.trimEnd());
  return [...before,'',closeNotice,'',...trailing].join('\n').trimEnd();
}
