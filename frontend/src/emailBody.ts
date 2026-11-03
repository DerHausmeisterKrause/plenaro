/**
 * Znuny returns MIME bodies as raw strings. HTML mails must never be injected
 * into the Plenaro document: render them in an opaque-origin sandbox instead.
 */
export function isHtmlMail(body: string, contentType?: string): boolean {
  if (/\b(?:text\/html|application\/xhtml\+xml)\b/i.test(contentType??'')) return true;
  if (/\btext\/plain\b/i.test(contentType??'')) return false;
  return /<!doctype\s+html\b|<\/?(?:html|head|body|div|p|table|tr|td|th|br|span|section|article|h[1-6]|ul|ol|li|blockquote)\b/i
    .test(body);
}

const csp = [
  "default-src 'none'",
  "base-uri 'none'",
  "object-src 'none'",
  "frame-src 'none'",
  "connect-src 'none'",
  "script-src 'none'",
  "form-action 'none'",
  "font-src 'none'",
  "img-src data:",
  "style-src 'unsafe-inline'",
  "navigate-to 'none'",
].join('; ');

/**
 * Embedded styles and data: images are allowed for email formatting.
 * Remote images/fonts/links, scripts, frames and network requests are blocked.
 * sandbox="" prevents script execution even if the HTML attempts to alter CSP.
 */
export function normalizeCid(value:string):string {
  return value.trim().replace(/^cid:/i,'').replace(/^<|>$/g,'').trim().toLowerCase();
}

export function referencedCids(body:string):string[] {
  const matches=[...body.matchAll(/\bsrc\s*=\s*(?:"cid:([^"]+)"|'cid:([^']+)')/gi)];
  return [...new Set(matches.map(match=>normalizeCid(match[1]??match[2]??'')))].filter(Boolean);
}

/** Replace only CID references occurring inside an HTML src attribute.
 * Unknown CIDs are blank instead of being treated as a remote network link.
 */
export function resolveCidImages(body:string,images:Readonly<Record<string,string>>):string {
  return body.replace(/\bsrc\s*=\s*(["'])cid:([^"']+)\1/gi,(_full,quote:string,id:string)=>{
    const data=images[normalizeCid(id)];
    // URI came from an authenticated Plenaro image endpoint, never from mail.
    const safe=data&&/^data:image\/(?:png|jpeg|gif|webp);base64,[A-Za-z0-9+/=]+$/i.test(data)
      ?data:'about:blank';
    return 'src='+quote+safe+quote;
  });
}

export function emailFrameDocument(body: string,
  cidImages:Readonly<Record<string,string>>={}): string {
  // Remove automatic navigations in addition to the iframe's sandbox/CSP.
  // The browser ultimately parses this as HTML INSIDE a separate sandboxed
  // document, never in the application's own DOM.
  const withoutRefresh = resolveCidImages(
    body.replace(/<meta\b[^>]*\bhttp-equiv\s*=\s*(?:["']?refresh["']?)[^>]*>/gi,''),cidImages);
  return `<!doctype html><html><head><meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="${csp}">
<meta name="referrer" content="no-referrer">
<style>
html {color-scheme:light; background:#fff}
body {box-sizing:border-box; margin:0; padding:14px; font:14px/1.55 Arial,Helvetica,sans-serif;
 color:#20242b; overflow-wrap:anywhere}
*,*::before,*::after {box-sizing:border-box}
table {max-width:100%;} img {max-width:100%;height:auto}
pre {white-space:pre-wrap;overflow-wrap:anywhere}
</style></head><body>${withoutRefresh}</body></html>`;
}
