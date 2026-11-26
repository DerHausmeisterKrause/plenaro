import {afterEach,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor,within} from '@testing-library/react';
import {ZnunyTicketConversationPanel} from './ZnunyTicketConversationPanel';
import {emailFrameDocument,normalizeCid,referencedCids,resolveCidImages} from './emailBody';
import {znunyAttachmentDownloadUrl} from './znunySyncApi';

afterEach(()=>{cleanup();vi.restoreAllMocks();vi.unstubAllGlobals();});

it('opens supported files as on-demand previews and resolves only requested CID images',async()=>{
  const requests:string[]=[];
  vi.stubGlobal('fetch',vi.fn(async (url:string)=>{
    requests.push(String(url));
    if(String(url).endsWith('/inline'))return {
      ok:true,status:200,json:async()=>({dataUri:'data:image/png;base64,aGVsbG8='})
    };
    if(String(url).endsWith('/articles'))return {
      ok:true,status:200,json:async()=>({
        ticketId:'77',ticketNumber:'77',title:'Test',articles:[{
          id:'12',subject:'Logo',from:'Kunde',to:'helpdesk',replyTo:'',
          body:'<p>Text</p><img src="cid:logo@beispiel.de"><img src="https://tracking.invalid/t.png">',
          contentType:'text/html',senderType:'customer',channel:'Email',
          created:'2026-11-02 12:00',attachments:[
            {id:'5',filename:'logo.png',contentType:'image/png',contentId:'<LOGO@BEISPIEL.DE>',
              size:123,disposition:'inline'},
            {id:'6',filename:'Angebot.pdf',contentType:'application/pdf',contentId:'',
              size:10240,disposition:'attachment'},
            {id:'7',filename:'Protokoll.txt',contentType:'text/plain',contentId:'',
              size:50,disposition:'attachment'},
          ]
        }]
      })
    };
    throw Error('Unexpected request: '+url);
  }));
  render(<ZnunyTicketConversationPanel ticketId="77" closed={true}/>);
  expect(await screen.findByRole('button',{name:/Angebot.pdf/})).toBeTruthy();
  const link=screen.getByRole('link',{name:/Protokoll.txt/});
  expect(link.getAttribute('href')).toBe('/api/v1/znuny/tickets/77/articles/12/attachments/7');
  expect(screen.getByText('Anhänge (3)')).toBeTruthy();
  const frame=await screen.findByTitle('HTML-E-Mail-Inhalt') as HTMLIFrameElement;
  await waitFor(()=>expect(frame.srcdoc).toContain('src="data:image/png;base64,aGVsbG8="'));
  expect(frame.getAttribute('sandbox')).toBe('');
  expect(frame.srcdoc).toContain('https://tracking.invalid/t.png');
  expect(frame.srcdoc).toContain("img-src data:");
  expect(requests.filter(url=>url.endsWith('/inline'))).toEqual([
    '/api/v1/znuny/tickets/77/articles/12/attachments/5/inline'
  ]);
  expect(requests).not.toContain(link.getAttribute('href'));
  expect(requests.some(url=>url.includes('/preview'))).toBe(false);
});

it('leaves unrecognized/missing CID references blank and rejects unsafe image URLs',()=>{
  expect(normalizeCid(' <IMG@Example.Org> ')).toBe('img@example.org');
  expect(referencedCids('<img src="cid:X@Host"><img src=\'cid:x@host\'>')).toEqual(['x@host']);
  const body='<img src="cid:safe"><img src="cid:unknown">';
  const rendered=resolveCidImages(body,{safe:'data:image/png;base64,YWJj'});
  expect(rendered).toContain('src="data:image/png;base64,YWJj"');
  expect(rendered).toContain('src="about:blank"');
  const malicious=resolveCidImages('<img src="cid:x">',{x:'javascript:alert(1)'});
  expect(malicious).not.toContain('javascript:');
  expect(emailFrameDocument(body)).not.toContain('src="cid:');
  expect(znunyAttachmentDownloadUrl('3','4','5'))
    .toBe('/api/v1/znuny/tickets/3/articles/4/attachments/5');
});


it('only displays approved image bytes, releases object URLs on close and requests a fresh preview on reopen',async()=>{
  const {AttachmentPreviewDialog}=await import('./AttachmentPreviewDialog');
  const createObjectURL=vi.fn(()=> 'blob:https://example.org/preview-test');
  const revokeObjectURL=vi.fn();
  const bitmap={width:2,height:1,close:vi.fn()};
  const decode=vi.fn().mockResolvedValue(bitmap);
  const drawImage=vi.fn();
  vi.stubGlobal('createImageBitmap',decode);
  vi.spyOn(HTMLCanvasElement.prototype,'getContext').mockReturnValue(
    {drawImage} as unknown as CanvasRenderingContext2D);
  vi.stubGlobal('URL',Object.assign(class extends URL {},{createObjectURL,revokeObjectURL}));
  const request=vi.fn().mockResolvedValue({
    ok:true,status:200,headers:new Headers({'Content-Type':'image/png'}),
    blob:async()=>new Blob([new Uint8Array([137,80,78,71,13,10,26,10])],{type:'image/png'}),
  });
  vi.stubGlobal('fetch',request);
  const props={endpoint:'/api/v1/znuny/tickets/77/articles/12/attachments/5/preview',
    attachment:{id:'5',filename:'Bild.png',contentType:'image/png',
      contentId:'',size:8,disposition:'attachment'},onClose:vi.fn()};
  const view=render(<AttachmentPreviewDialog {...props}/>);
  const dialog=await screen.findByRole('dialog',{name:'Anhang-Vorschau'});
  const canvas=await within(dialog).findByRole('img',{name:'Bild.png'}) as HTMLCanvasElement;
  expect(canvas.tagName).toBe('CANVAS');
  expect(canvas.width).toBe(2);
  expect(drawImage).toHaveBeenCalledWith(bitmap,0,0,2,1);
  expect(decode).toHaveBeenCalledWith(expect.any(Blob));
  expect(bitmap.close).toHaveBeenCalled();
  expect(within(dialog).getByText('ClamAV-Prüfung ohne Fund')).toBeTruthy();
  expect(request).toHaveBeenCalledWith(props.endpoint,expect.objectContaining({cache:'no-store'}));
  fireEvent.click(within(dialog).getByRole('button',{name:'Vorschau schließen'}));
  expect(props.onClose).toHaveBeenCalledTimes(1);
  view.unmount();
  expect(revokeObjectURL).toHaveBeenCalledWith('blob:https://example.org/preview-test');
  render(<AttachmentPreviewDialog {...props}/>);
  await screen.findByRole('img',{name:'Bild.png'});
  expect(request).toHaveBeenCalledTimes(2);
});

it.each([422,503])('fails closed and never renders attachment bytes after HTTP %s',async(status)=>{
  const {AttachmentPreviewDialog}=await import('./AttachmentPreviewDialog');
  const readBytes=vi.fn();
  vi.stubGlobal('fetch',vi.fn().mockResolvedValue({
    ok:false,status,json:async()=>({error:'Virenscan blockiert.'}),blob:readBytes,
  }));
  render(<AttachmentPreviewDialog endpoint="/api/v1/znuny/tickets/77/articles/12/attachments/5/preview"
    attachment={{id:'5',filename:'Bild.png',contentType:'image/png',size:8,contentId:'',disposition:'attachment'}}
    onClose={()=>{}}/>);
  expect(await screen.findByText('Virenscan blockiert.')).toBeTruthy();
  expect(screen.queryByRole('img',{name:'Bild.png'})).toBeNull();
  expect(screen.queryByRole('link',{name:'Geprüfte Datei herunterladen'})).toBeNull();
  expect(readBytes).not.toHaveBeenCalled();
});

it('reports a decoding error separately from the successful scanner verdict',async()=>{
  const {AttachmentPreviewDialog}=await import('./AttachmentPreviewDialog');
  vi.stubGlobal('URL',Object.assign(class extends URL {},{
    createObjectURL:vi.fn(()=> 'blob:https://example.org/preview-test'),revokeObjectURL:vi.fn(),
  }));
  vi.stubGlobal('createImageBitmap',vi.fn().mockRejectedValue(new Error('Invalid image')));
  vi.stubGlobal('fetch',vi.fn().mockResolvedValue({
    ok:true,status:200,headers:new Headers({'Content-Type':'image/png'}),
    blob:async()=>new Blob([new Uint8Array([137,80,78,71,13,10,26,10])],{type:'image/png'}),
  }));
  render(<AttachmentPreviewDialog endpoint="/api/v1/znuny/tickets/77/articles/12/attachments/5/preview"
    attachment={{id:'5',filename:'Bild.png',contentType:'image/png',size:8,contentId:'',disposition:'attachment'}}
    onClose={()=>{}}/>);
  expect(await screen.findByText(/geprüften Bilddaten nicht dekodieren oder darstellen/)).toBeTruthy();
  expect(screen.getByText('ClamAV-Prüfung ohne Fund')).toBeTruthy();
  expect(screen.queryByRole('img',{name:'Bild.png'})).toBeNull();
});
