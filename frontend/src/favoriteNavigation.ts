/**
 * Microsoft 365 document links require the real browser origin for SSO,
 * Office web apps and cookies. Do not send them to the shared reverse proxy.
 * Match DNS suffixes at label boundaries, not arbitrary substring matches.
 */
export function microsoftDocumentUrl(value?:string|null):string|null {
  if(!value)return null;
  try {
    const url=new URL(value);
    if(url.protocol!=='https:'||url.port||url.username||url.password)return null;
    const host=url.hostname.toLowerCase();
    const sharePoint=['sharepoint.com','sharepoint.us','sharepoint.de','sharepoint.cn']
      .some(domain=>host===domain||host.endsWith('.'+domain));
    return sharePoint||host==='1drv.ms'||host==='onedrive.live.com'?url.href:null;
  }catch{return null;}
}
